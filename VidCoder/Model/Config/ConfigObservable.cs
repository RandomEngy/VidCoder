using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoder.Model;

public class ConfigObservable<T> : IObservable<T>
{
	private string key;
	private List<IObserver<T>> observers = new();
	private readonly object disposeLock = new();

	public ConfigObservable(string key)
	{
		this.key = key;
	} 

	public IDisposable Subscribe(IObserver<T> observer)
	{
		observer.OnNext(Config.Get<T>(this.key));

		lock (this.disposeLock)
		{
			this.observers.Add(observer);
		}

		return new ConfigObservableDisposeToken<T>(this.observers, observer, disposeLock);
	}

	public void OnNext(T value)
	{
		lock (this.disposeLock)
		{
			foreach (IObserver<T> observer in this.observers)
			{
				observer.OnNext(value);
			}
		}
	}

	private class ConfigObservableDisposeToken<T1> : IDisposable
	{
		private List<IObserver<T1>> observers;
		private IObserver<T1> observer;
		private readonly object disposeLock;
		private bool disposed = false;

		public ConfigObservableDisposeToken(List<IObserver<T1>> observers, IObserver<T1> observer, object disposeLock)
		{
			this.observers = observers;
			this.observer = observer;
			this.disposeLock = disposeLock;
		}

		public void Dispose()
		{
			lock (this.disposeLock)
			{
				if (!this.disposed)
				{
					this.observers.Remove(this.observer);
					this.disposed = true;
				}
			}
		}
	}
}
