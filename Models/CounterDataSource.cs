using System;
using System.Windows;
using System.Diagnostics;

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
		}

		public float NextValue()
		{
			return _counter.NextValue();
		}

		public void Dispose()
		{
			_counter?.Dispose();
		}
	}
}
