using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Windows;

namespace VidCoder;

public class RefireControl
{
	/// <summary>
	/// How long stages last.
	/// </summary>
	private const int StageDurationMsec = 900;

	// At each stage the refire rate increases.
	private static readonly List<int> Delays = new List<int> {500, 200, 100, 50, 20};

	private Action refireAction;
	private Timer refireTimer;

	private Stopwatch stopwatch;
	private object refireSync = new object();
	private bool running = false;

	public RefireControl(Action refireAction)
	{
		this.refireAction = refireAction;
	}

	public void Begin()
	{
		lock (this.refireSync)
		{
			this.stopwatch = Stopwatch.StartNew();
			this.running = true;

			// Fire once immediately.
			refireAction();

			this.refireTimer = new Timer(
				obj =>
					{
						lock (this.refireSync)
						{
							if (this.running)
							{
								int stage = (int) (this.stopwatch.ElapsedMilliseconds / StageDurationMsec);
								int newDelay;

								if (stage >= Delays.Count)
								{
									newDelay = Delays[Delays.Count - 1];
								}
								else
								{
									newDelay = Delays[stage];
								}

								Application.Current.Dispatcher.BeginInvoke(refireAction);

								this.refireTimer.Change(newDelay, newDelay);
							}
						}
					},
				null,
				Delays[0],
				Delays[0]);
		}
	}

	public void Stop()
	{
		lock (this.refireSync)
		{
			this.stopwatch.Stop();
			this.running = false;

			if (this.refireTimer != null)
			{
				this.refireTimer.Dispose();
			}
		}
	}
}
