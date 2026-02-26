using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using PerformanceTrayMonitor.Models;

namespace PerformanceTrayMonitor.Configuration
{
	internal sealed class DefaultSettingsProvider
	{
		public SettingsOptions Create()
		{
			var cat = new PerformanceCounterCategory("PhysicalDisk");
			var instances = cat.GetInstanceNames();

			var counters = (instances.Length == 0)
				? cat.GetCounters().Select(c => c.CounterName)
				: instances.SelectMany(inst => cat.GetCounters(inst)).Select(c => c.CounterName);

			var firstCounter = CounterConfig.DefaultCounter;

			return new SettingsOptions(
				Counters: new List<CounterSettingsDto>
				{
					new CounterSettingsDto
					{
						Id = Guid.NewGuid(),
						Category = CounterConfig.DefaultCategory,
						Counter = CounterConfig.DefaultCounter,
						Instance = instances.Contains("_Total") ? "_Total" : instances.FirstOrDefault() ?? "",
						DisplayName = CounterConfig.DefaultDisplayName,
						Min = CounterConfig.DefaultMin,
						Max = CounterConfig.DefaultMax,
						Mode = CounterConfig.DefaultMode,
						ShowInTray = false,
						IconSet = CounterConfig.DefaultIconSet
					}
				},
				Version: 2
			);
		}
	}
}
