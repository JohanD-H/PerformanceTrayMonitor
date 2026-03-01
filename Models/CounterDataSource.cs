using System;
using System.Diagnostics;
using Serilog;

// ------------------------------------
// Counter data
// ------------------------------------
namespace PerformanceTrayMonitor.Models
{
	public class CounterDataSource : IDisposable
	{
		private readonly PerformanceCounter _counter;

		public CounterDataSource(CounterSettings settings)
		{
			_counter = new PerformanceCounter(
				settings.Category,
				settings.Counter,
				settings.Instance,
				readOnly: true);
			Log.Debug($"Counter = {settings.Counter}");
		}

		public float NextValue()
		{
			Log.Debug($"Next value: {_counter.NextValue}");
			return _counter.NextValue();
		}

		public void Dispose()
		{
			_counter?.Dispose();
		}
	}
}
