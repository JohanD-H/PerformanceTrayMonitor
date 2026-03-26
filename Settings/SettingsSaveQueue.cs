using PerformanceTrayMonitor.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PerformanceTrayMonitor.Settings
{
	public static class SettingsSaveQueue
	{
		private static readonly object _lock = new();
		private static SettingsDto _pending;
		private static CancellationTokenSource _cts;

		public static void Enqueue(SettingsDto dto)
		{
			lock (_lock)
			{
				_pending = dto;
				_cts?.Cancel();
				_cts = new CancellationTokenSource();
				var token = _cts.Token;

				_ = Task.Run(async () =>
				{
					try
					{
						await Task.Delay(300, token); // debounce window
						if (!token.IsCancellationRequested)
							SettingsRepository.SaveAtomic(_pending);
					}
					catch (TaskCanceledException) { }
				});
			}
		}

		public static void Flush()
		{
			lock (_lock)
			{
				_cts?.Cancel();
				if (_pending != null)
					SettingsRepository.SaveAtomic(_pending);
			}
		}
	}
}
