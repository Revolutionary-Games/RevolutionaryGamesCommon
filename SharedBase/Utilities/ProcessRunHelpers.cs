namespace SharedBase.Utilities;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public static class ProcessRunHelpers
{
    public const int EXIT_STATUS_UNAVAILABLE = 173;

    /// <summary>
    ///   How long to wait after a process has exited before notifying it is done. This is a fallback for if the
    ///   process output reading code never detects the end of output.
    /// </summary>
    private static readonly TimeSpan TimeToWaitForProcessOutput = TimeSpan.FromMilliseconds(1000);

    [UnsupportedOSPlatform("browser")]
    public static Task<ProcessResult> RunProcessAsync(ProcessStartInfo startInfo,
        CancellationToken cancellationToken, bool captureOutput = true, int startRetries = 5,
        bool waitForLastOutput = true, Encoding? inputOutputEncoding = null)
    {
        return RunProcessAsync(startInfo, cancellationToken, captureOutput, true, startRetries, waitForLastOutput,
            inputOutputEncoding);
    }

    [UnsupportedOSPlatform("browser")]
    public static Task<ProcessResult> RunProcessAsync(ProcessStartInfo startInfo,
        CancellationToken cancellationToken, bool captureOutput = true, bool captureStdErrorInStdOut = true,
        int startRetries = 5, bool waitForLastOutput = true, Encoding? inputOutputEncoding = null)
    {
        while (true)
        {
            try
            {
                return StartProcessInternal(startInfo, cancellationToken, null, captureOutput, captureStdErrorInStdOut,
                    null, null, waitForLastOutput, inputOutputEncoding).Task;
            }
            catch (InvalidOperationException)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (startRetries-- > 0)
                    continue;

                throw;
            }
        }
    }

    [UnsupportedOSPlatform("browser")]
    public static Task<ProcessResult> RunProcessWithOutputStreamingAsync(ProcessStartInfo startInfo,
        CancellationToken cancellationToken, Action<string> onOutput, Action<string> onErrorOut, int startRetries = 5,
        bool waitForLastOutput = true, Encoding? inputOutputEncoding = null)
    {
        while (true)
        {
            try
            {
                return StartProcessInternal(startInfo, cancellationToken, null, true, false, onOutput, onErrorOut,
                    waitForLastOutput, inputOutputEncoding).Task;
            }
            catch (InvalidOperationException)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (startRetries-- > 0)
                    continue;

                throw;
            }
        }
    }

    [UnsupportedOSPlatform("browser")]
    public static Task<ProcessResult> RunProcessWithStdInAndOutputStreamingAsync(ProcessStartInfo startInfo,
        CancellationToken cancellationToken, IReadOnlyCollection<string> inputLines, Action<string> onOutput,
        Action<string> onErrorOut, int startRetries = 5, bool waitForLastOutput = true,
        Encoding? inputOutputEncoding = null)
    {
        while (true)
        {
            try
            {
                return StartProcessInternal(startInfo, cancellationToken, inputLines, true, false, onOutput, onErrorOut,
                    waitForLastOutput, inputOutputEncoding).Task;
            }
            catch (InvalidOperationException)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (startRetries-- > 0)
                    continue;

                throw;
            }
        }
    }

    /// <summary>
    ///   Adds a new folder to the PATH variable for a process start info if missing
    /// </summary>
    /// <param name="startInfo">The start info to modify</param>
    /// <param name="extraPathItem">The PATH item that needs to exist</param>
    /// <param name="includeCurrentPath">
    ///   If true the current PATH environment variable is read as a default value
    /// </param>
    /// <returns>True if the PATH was modified, false if no modification was necessary</returns>
    /// <exception cref="ArgumentException">If the extra folder doesn't exist</exception>
    [UnsupportedOSPlatform("browser")]
    public static bool AddToPathInStartInfo(ProcessStartInfo startInfo, string extraPathItem,
        bool includeCurrentPath = true)
    {
        if (!Directory.Exists(extraPathItem))
            throw new ArgumentException("Folder to add to PATH must exist", nameof(extraPathItem));

        var defaultValue = new KeyValuePair<string, string?>("PATH",
            includeCurrentPath ? Environment.GetEnvironmentVariable("PATH") ?? string.Empty : string.Empty);

        var path = startInfo.Environment.FirstOrDefault(p => p.Key.ToUpperInvariant() == "PATH", defaultValue);

        // Don't need to do anything if already exists
        if (path.Value?.Contains(extraPathItem) == true)
            return false;

        string newValue;

        if (string.IsNullOrEmpty(path.Value))
        {
            newValue = $"{extraPathItem}";
        }
        else
        {
            newValue = $"{extraPathItem}{Path.PathSeparator}{path.Value}";
        }

        startInfo.Environment[path.Key] = newValue;
        return true;
    }

    /// <summary>
    ///   The internal process running function
    /// </summary>
    /// <param name="startInfo">The setup process we should start</param>
    /// <param name="cancellationToken">Cancels the process running</param>
    /// <param name="inputLines">Lines to send to the process standard input</param>
    /// <param name="captureOutput">
    ///   If true the output of the process is redirected to us rather than whatever terminal environment
    ///   we are running in
    /// </param>
    /// <param name="captureErrorToStdOut">
    ///   If true then when output lines are captured they are also passed to the stdout capturing. If
    ///   <see cref="captureOutput"/> is false or there's custom actions (<see cref="onOutput"/>) defined, this
    ///   parameter does nothing.
    /// </param>
    /// <param name="onOutput">
    ///   If specified will be called with each output line (without line terminator), instead of pooling the data
    ///   to the process results.
    /// </param>
    /// <param name="onErrorOut">Same as <see cref="onOutput"/> but for the standard error stream</param>
    /// <param name="waitForLastOutput">
    ///   If true will wait for last outputs to be received once process exits.
    ///   If false, will immediately become ready. Only does something if <see cref="captureOutput"/> is true.
    /// </param>
    /// <param name="inputOutputEncoding">
    ///   The encoding used for the input and output streams when they are redirected
    /// </param>
    /// <returns>A task completion source with a task that finishes with the process result</returns>
    /// <exception cref="InvalidOperationException">When process can't be started properly</exception>
    /// <remarks>
    ///   <para>
    ///     This is a very complex function, but trying to split this into variants doesn't seem that maintainable or
    ///     short either. So at least this is documented.
    ///   </para>
    /// </remarks>
    [UnsupportedOSPlatform("browser")]
    private static TaskCompletionSource<ProcessResult> StartProcessInternal(ProcessStartInfo startInfo,
        CancellationToken cancellationToken, IEnumerable<string>? inputLines, bool captureOutput,
        bool captureErrorToStdOut, Action<string>? onOutput, Action<string>? onErrorOut, bool waitForLastOutput,
        Encoding? inputOutputEncoding)
    {
        inputOutputEncoding ??= new UTF8Encoding(false);

        if (captureOutput)
        {
            startInfo.StandardOutputEncoding = inputOutputEncoding;
            startInfo.RedirectStandardOutput = true;

            startInfo.StandardErrorEncoding = inputOutputEncoding;
            startInfo.RedirectStandardError = true;
        }

        if (inputLines != null)
        {
            startInfo.StandardInputEncoding = inputOutputEncoding;
            startInfo.RedirectStandardInput = true;
        }

        var result = new ProcessResult(captureErrorToStdOut);
        var taskCompletionSource = new TaskCompletionSource<ProcessResult>();

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true,
        };

        var waitSkipSource = SetupProcessExitAndCancelEvents(result, process, taskCompletionSource,
            captureOutput && waitForLastOutput, cancellationToken);

        if (captureOutput)
        {
            if (onOutput != null && onErrorOut != null)
            {
                SetupProcessOutputReadWithCallbacks(result, process, waitSkipSource, onOutput, onErrorOut);
            }
            else
            {
                SetupBufferedProcessOutputRead(result, process, captureErrorToStdOut, waitSkipSource);
            }
        }
        else
        {
            result.PendingStreamsToEnd = 0;
        }

        // There might still be a small race condition chance between the cancel check task and the process start
        // call (if the cancellation token is cancelled after the next line but before the process is started the
        // process cancellation task won't be able to cancel the task that didn't start yet)
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            if (!process.Start())
                throw new InvalidOperationException($"Could not start process: {process}");
        }
        catch (Exception)
        {
            // Stop the wait task from waiting forever by marking this as exited
            result.Exited = true;
            result.ExitCode = -1;
            throw;
        }

        if (captureOutput)
            StartProcessOutputRead(process, cancellationToken);

        if (inputLines != null)
        {
            _ = WriteInputLines(result, process, inputLines, cancellationToken);
        }

        return taskCompletionSource;
    }

    [UnsupportedOSPlatform("browser")]
    private static TaskCompletionSource? SetupProcessExitAndCancelEvents(ProcessResult result, Process process,
        TaskCompletionSource<ProcessResult> taskCompletionSource, bool hasCapturedStreams,
        CancellationToken cancellationToken)
    {
        TaskCompletionSource? waitSkipSource = null;
        if (hasCapturedStreams)
            waitSkipSource = new TaskCompletionSource();

        void HandleExit()
        {
            try
            {
                process.Dispose();
            }
            catch (Exception)
            {
                result.ErrorDisposingProcessOrSettingResult = true;
            }

            if (!taskCompletionSource.TrySetResult(result))
            {
                // This should only happen if the process was canceled
                result.ErrorDisposingProcessOrSettingResult = true;
            }
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
            try
            {
                result.ExitCode = process.ExitCode;
            }
            catch (Exception)
            {
                result.ExitCode = EXIT_STATUS_UNAVAILABLE;
            }

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

        return waitSkipSource;
    }

    [UnsupportedOSPlatform("browser")]
    private static void SetupBufferedProcessOutputRead(ProcessResult result, Process process, bool includeErrorInStdOut,
        TaskCompletionSource? waitSkipSource)
    {
        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data == null)
            {
                // Stream ended, notify other places (if we were the last stream)
                if (Interlocked.Decrement(ref result.PendingStreamsToEnd) == 0)
                    waitSkipSource?.TrySetResult();

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
                    waitSkipSource?.TrySetResult();

                return;
            }

            // This makes error output appear logically in the place where it is supposed to
            if (includeErrorInStdOut)
            {
                result.StdOut.Append(args.Data);
                result.StdOut.Append('\n');
            }

            result.ErrorOut.Append(args.Data);
            result.ErrorOut.Append('\n');
        };
    }

    [UnsupportedOSPlatform("browser")]
    private static void SetupProcessOutputReadWithCallbacks(ProcessResult result, Process process,
        TaskCompletionSource? waitSkipSource, Action<string> onOutput, Action<string> onErrorOut)
    {
        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data == null)
            {
                if (Interlocked.Decrement(ref result.PendingStreamsToEnd) == 0)
                    waitSkipSource?.TrySetResult();

                return;
            }

            onOutput.Invoke(args.Data);
        };

        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data == null)
            {
                if (Interlocked.Decrement(ref result.PendingStreamsToEnd) == 0)
                    waitSkipSource?.TrySetResult();

                return;
            }

            onErrorOut.Invoke(args.Data);
        };
    }

    [UnsupportedOSPlatform("browser")]
    private static void StartProcessOutputRead(Process process, CancellationToken cancellationToken)
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
    private static async Task WriteInputLines(ProcessResult result, Process process, IEnumerable<string> lines,
        CancellationToken cancellationToken)
    {
        foreach (var line in lines)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            if (process.HasExited)
            {
                return;
            }

            await process.StandardInput.WriteLineAsync(line);
        }

        result.AllInputLinesWritten = true;

        if (cancellationToken.IsCancellationRequested)
            return;

        try
        {
            process.StandardInput.Close();
        }
        catch (Exception)
        {
            result.ErrorInInputLineClosing = true;
        }
    }

    public class ProcessResult
    {
        /// <summary>
        ///   For internal use to detect when all output streams are closed
        /// </summary>
        internal int PendingStreamsToEnd = 2;

        private readonly bool stdoutIncludesError;

        /// <summary>
        ///   Creates a new process result
        /// </summary>
        /// <param name="stdoutIncludesError">
        ///   If true then it is assumed that <see cref="StdOut"/> also gets the
        ///   <see cref="ErrorOut"/> interleaved in it (and it won't be separately included in the
        ///   <see cref="FullOutput"/>.
        /// </param>
        public ProcessResult(bool stdoutIncludesError)
        {
            this.stdoutIncludesError = stdoutIncludesError;
        }

        public int ExitCode { get; set; }

        public StringBuilder StdOut { get; set; } = new();
        public StringBuilder ErrorOut { get; set; } = new();

        public string Output => StdOut.ToString();

        /// <summary>
        ///   The full output of this process. If the error stream was not interleaved then it is appended after the
        ///   normal output
        /// </summary>
        public string FullOutput
        {
            get
            {
                if (stdoutIncludesError || ErrorOut.Length < 1)
                    return StdOut.ToString();

                return $"{StdOut}\n{ErrorOut}";
            }
        }

        /// <summary>
        ///   True once process has exited and <see cref="ExitCode"/> is available. Most code should rely on waiting
        ///   on the task to know when it is complete, but this variable may prove useful in some cases.
        /// </summary>
        public bool Exited { get; set; }

        // The following are only kind of useful flags to see if something went wrong. These are mostly really for
        // internal use, though

        public bool AllInputLinesWritten { get; internal set; }

        public bool OutputWaitingTimedOut { get; internal set; }

        public bool ErrorInInputLineClosing { get; internal set; }

        public bool ErrorDisposingProcessOrSettingResult { get; internal set; }
    }
}
