namespace SharedBase.Utilities;

using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public static class ProcessRunHelpers
{
    /// <summary>
    ///   How long to wait after a process has exited before notifying it is done. This is a fallback for if the
    ///   process output reading code never detects the end of output.
    /// </summary>
    private static readonly TimeSpan TimeToWaitForProcessOutput = TimeSpan.FromMilliseconds(1000);

    [UnsupportedOSPlatform("browser")]
    public static Task<ProcessResult> RunProcessAsync(ProcessStartInfo startInfo,
        CancellationToken cancellationToken, bool captureOutput = true, int startRetries = 5,
        bool waitForLastOutput = true)
    {
        if (captureOutput)
        {
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
        }

        try
        {
            return StartProcessInternal(startInfo, cancellationToken, captureOutput, waitForLastOutput).Task;
        }
        catch (InvalidOperationException)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (startRetries > 0)
            {
                return RunProcessAsync(startInfo, cancellationToken, captureOutput, startRetries - 1);
            }

            throw;
        }
    }

    [UnsupportedOSPlatform("browser")]
    public static void StartProcessOutputRead(Process process, CancellationToken cancellationToken)
    {
        if (process == null)
            throw new ArgumentException("Process must not be null for starting output read");

        const int retries = 5;

        // For some reason it seems that this sometimes fails with "System.InvalidOperationException:
        // StandardOut has not been redirected or the process hasn't started yet." So this is retried a
        // few times
        bool success = false;
        for (int i = 0; i < retries; ++i)
        {
            try
            {
                process.BeginOutputReadLine();
                success = true;
                break;
            }
            catch (Exception e) when (e is InvalidOperationException or NullReferenceException)
            {
                if (cancellationToken.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(15 * (i + 1))))
                    cancellationToken.ThrowIfCancellationRequested();
            }
        }

        if (!success)
            throw new InvalidOperationException("Failed to BeginOutputReadLine even after a few retries");

        cancellationToken.ThrowIfCancellationRequested();

        success = false;
        for (int i = 0; i < retries; ++i)
        {
            try
            {
                process.BeginErrorReadLine();
                success = true;
                break;
            }
            catch (Exception e) when (e is InvalidOperationException or NullReferenceException)
            {
                if (cancellationToken.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(15 * (i + 1))))
                    cancellationToken.ThrowIfCancellationRequested();
            }
        }

        if (!success)
            throw new InvalidOperationException("Failed to BeginErrorReadLine even after a few retries");
    }

    [UnsupportedOSPlatform("browser")]
    private static TaskCompletionSource<ProcessResult> StartProcessInternal(ProcessStartInfo startInfo,
        CancellationToken cancellationToken, bool captureOutput, bool waitForLastOutput)
    {
        var result = new ProcessResult();
        var taskCompletionSource = new TaskCompletionSource<ProcessResult>();

        TaskCompletionSource? waitSkipSource = null;
        if (captureOutput && waitForLastOutput)
            waitSkipSource = new TaskCompletionSource();

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true,
        };

        void HandleExit()
        {
            process.Dispose();
            taskCompletionSource.SetResult(result);
        }

        async void WaitBeforeExiting()
        {
            try
            {
                Thread.Yield();
                var waitSkip = waitSkipSource.Task;
                await waitSkip.WaitAsync(TimeToWaitForProcessOutput, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // This is ignored as we are done anyway and can just set the result to get out of this code fast
            }
            catch (TimeoutException)
            {
                result.OutputWaitingTimedOut = true;
            }

            HandleExit();
        }

        process.Exited += (_, _) =>
        {
            result.ExitCode = process.ExitCode;
            result.Exited = true;

            // Wait for last bits of output if wanted. This is because very short running programs often miss out on
            // their last bit of output otherwise
            if (waitSkipSource != null)
            {
                // Give some extra time before we end to get the last bit of output processed. Or until the output
                // notifies we are done
                var task = new Task(WaitBeforeExiting);
                task.Start();
            }
            else
            {
                HandleExit();
            }
        };

        // Timer based cancellation check to allow canceling even when there is no output (or we aren't reading output)
        async void CancelWithTimer()
        {
            while (true)
            {
                if (result.Exited)
                    break;

                bool canceled;
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

                    canceled = cancellationToken.IsCancellationRequested;
                }
                catch (OperationCanceledException)
                {
                    canceled = true;
                }

                if (result.Exited)
                    break;

                if (canceled)
                {
                    try
                    {
                        // Try to kill the process to cancel it. We report cancellation first to make sure we don't
                        // get stuck, managing to kill the running process is less important
                        taskCompletionSource.SetCanceled(cancellationToken);
                        process.Kill();
                    }
                    catch (Exception)
                    {
                        break;
                    }
                }
            }
        }

        var cancelCheckTask = new Task(CancelWithTimer);
        cancelCheckTask.Start();

        if (captureOutput)
        {
            process.OutputDataReceived += (_, args) =>
            {
                if (args.Data == null)
                {
                    // Stream ended, notify other places
                    if (Interlocked.Decrement(ref result.PendingStreamsToEnd) == 0)
                    {
                        waitSkipSource?.TrySetResult();
                    }

                    return;
                }

                result.StdOut.Append(args.Data);
                result.StdOut.Append('\n');
            };
            process.ErrorDataReceived += (_, args) =>
            {
                if (args.Data == null)
                {
                    if (Interlocked.Decrement(ref result.PendingStreamsToEnd) == 0)
                    {
                        waitSkipSource?.TrySetResult();
                    }

                    return;
                }

                result.ErrorOut.Append(args.Data);
                result.ErrorOut.Append('\n');
            };
        }
        else
        {
            result.PendingStreamsToEnd = 0;
        }

        if (!process.Start())
            throw new InvalidOperationException($"Could not start process: {process}");

        if (captureOutput)
        {
            StartProcessOutputRead(process, cancellationToken);
        }

        return taskCompletionSource;
    }

    public class ProcessResult
    {
        /// <summary>
        ///   For internal use to detect when all output streams are closed
        /// </summary>
        internal int PendingStreamsToEnd = 2;

        public int ExitCode { get; set; }

        public StringBuilder StdOut { get; set; } = new();
        public StringBuilder ErrorOut { get; set; } = new();

        public string Output => StdOut.ToString();

        public string FullOutput
        {
            get
            {
                if (ErrorOut.Length < 1)
                    return StdOut.ToString();

                return $"{StdOut}\n{ErrorOut}";
            }
        }

        /// <summary>
        ///   True once process has exited and <see cref="ExitCode"/> is available. Most code should rely on waiting
        ///   on the task to know when it is complete, but this variable may prove useful in some cases.
        /// </summary>
        public bool Exited { get; set; }

        public bool OutputWaitingTimedOut { get; set; }
    }
}
