using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoder.Model
{
	public class ConfigObservable<T> : IObservable<T>
	{
		private string key;
		private List<IObserver<T>> observers = new List<IObserver<T>>();

		public ConfigObservable(string key)
		{
			this.key = key;
		} 

		public IDisposable Subscribe(IObserver<T> observer)
		{
			observer.OnNext(Config.Get<T>(this.key));
			this.observers.Add(observer);

			return new ConfigObservableDisposeToken<T>(this.observers, observer);
		}

		public void OnNext(T value)
		{
			foreach (IObserver<T> observer in this.observers)
			{
				observer.OnNext(value);
			}
		}

		private class ConfigObservableDisposeToken<T1> : IDisposable
		{
			private List<IObserver<T1>> observers;
			private IObserver<T1> observer;
			private readonly object disposeLock = new object();
			private bool disposed = false;

			public ConfigObservableDisposeToken(List<IObserver<T1>> observers, IObserver<T1> observer)
			{
				this.observers = observers;
				this.observer = observer;
			}

			public void Dispose()
			{
				lock (this.disposeLock)
				{
					if (!this.disposed)
					{
						if (this.observers.Contains(this.observer))
						{
							this.observers.Remove(this.observer);
						}

						this.disposed = true;
					}
				}
			}
		}
	}
}
