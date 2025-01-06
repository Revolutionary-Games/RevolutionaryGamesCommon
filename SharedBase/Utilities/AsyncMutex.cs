namespace SharedBase.Utilities;

using System;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
///   Implements an async/await compatible <see cref="Mutex"/> for use by async code. Otherwise, using a plain Mutex
///   will not work correctly in those contexts. Note that this class itself is not thread safe so only use for
///   inter-process synchronization and now between tasks.
/// </summary>
/// <remarks>
///    <para>
///      This code is from: https://gist.github.com/dfederm/35c729f6218834b764fa04c219181e4e and is explained in:
///      https://dfederm.com/async-mutex/ There's no attached license information but hopefully here with a few
///      modifications this is completely fine for us to include here in our code.
///    </para>
/// </remarks>
[UnsupportedOSPlatform("browser")]
public sealed class AsyncMutex : IAsyncDisposable
{
    private readonly string mutexName;
    private Task? mutexTask;
    private ManualResetEventSlim? releaseEvent;
    private CancellationTokenSource? cancellationTokenSource;

    public AsyncMutex(string name)
    {
        mutexName = name;
    }

    public Task AcquireAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        TaskCompletionSource taskCompletionSource = new();

        releaseEvent = new ManualResetEventSlim();
        cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Putting all mutex manipulation in its own task as it doesn't work in async contexts
        // Note: this task should not throw.
        mutexTask = Task.Factory.StartNew(() =>
        {
            try
            {
                CancellationToken taskInternalCancellation = cancellationTokenSource.Token;
                using var mutex = new Mutex(false, mutexName);
                try
                {
                    // Wait for either the mutex to be acquired, or cancellation
                    if (WaitHandle.WaitAny([mutex, taskInternalCancellation.WaitHandle]) != 0)
                    {
                        taskCompletionSource.SetCanceled(taskInternalCancellation);
                        return;
                    }
                }
                catch (AbandonedMutexException)
                {
                    // Abandoned by another process, we acquired it.
                }

                taskCompletionSource.SetResult();

                // Wait until the release call
                releaseEvent.Wait();

                mutex.ReleaseMutex();
            }
            catch (OperationCanceledException)
            {
                taskCompletionSource.TrySetCanceled(cancellationToken);
            }
            catch (Exception ex)
            {
                taskCompletionSource.TrySetException(ex);
            }
        }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        return taskCompletionSource.Task;
    }

    public async Task ReleaseAsync()
    {
        releaseEvent?.Set();

        if (mutexTask != null)
        {
            await mutexTask;
        }
    }

    public async ValueTask DisposeAsync()
    {
        // Ensure the mutex task stops waiting for any acquire
        cancellationTokenSource?.Cancel();

        // Ensure the mutex is released
        await ReleaseAsync();

        releaseEvent?.Dispose();
        cancellationTokenSource?.Dispose();
    }
}
