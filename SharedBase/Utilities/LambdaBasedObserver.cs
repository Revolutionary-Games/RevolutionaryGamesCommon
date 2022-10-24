namespace SharedBase.Utilities;

using System;

/// <summary>
///   Observer that triggers a lambda when notified
/// </summary>
/// <typeparam name="T">The type of value this can observe</typeparam>
public class LambdaBasedObserver<T> : IObserver<T>
{
    private readonly Action<T> onNext;
    private readonly Action<Exception>? onError;
    private readonly Action? onCompleted;

    public LambdaBasedObserver(Action<T> onNext, Action<Exception>? onError = null, Action? onCompleted = null)
    {
        this.onNext = onNext;
        this.onError = onError;
        this.onCompleted = onCompleted;
    }

    public void OnCompleted()
    {
        onCompleted?.Invoke();
    }

    public void OnError(Exception error)
    {
        onError?.Invoke(error);
    }

    public void OnNext(T value)
    {
        onNext.Invoke(value);
    }
}
