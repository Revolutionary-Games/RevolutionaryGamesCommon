namespace SharedBase.Models;

using System;
using System.Collections.Generic;

/// <summary>
///   A single value that can be observed
/// </summary>
/// <typeparam name="T">The type of the value</typeparam>
public class ObservableValue<T> : IObservable<T>
    where T : IComparable<T>
{
    private readonly List<IObserver<T>> observers = new();

    private T currentValue;

    public ObservableValue(T initialValue)
    {
        currentValue = initialValue;
    }

    public T Value
    {
        get => currentValue;
        set
        {
            if (value.Equals(currentValue))
                return;

            currentValue = value;
            Notify();
        }
    }

    public IDisposable Subscribe(IObserver<T> observer)
    {
        if (!observers.Contains(observer))
            observers.Add(observer);

        return new Subscription(observers, observer);
    }

    private void Notify()
    {
        foreach (var observer in observers)
        {
            observer.OnNext(currentValue);
        }
    }

    private class Subscription : IDisposable
    {
        private readonly List<IObserver<T>> observers;
        private readonly IObserver<T> observer;

        public Subscription(List<IObserver<T>> observers, IObserver<T> observer)
        {
            this.observers = observers;
            this.observer = observer;
        }

        public void Dispose()
        {
            observers.Remove(observer);
        }
    }
}
