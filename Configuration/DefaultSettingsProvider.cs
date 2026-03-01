using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using PerformanceTrayMonitor.Models;

namespace PerformanceTrayMonitor.Configuration
{
	internal sealed class DefaultSettingsProvider
	{
		public CounterSettingsDto CreateDefaultCounter()
		{
			string instance = "";

			try
			{
				var cat = new PerformanceCounterCategory("PhysicalDisk");
				var instances = cat.GetInstanceNames();

				instance = instances.Contains("_Total")
					? "_Total"
					: instances.FirstOrDefault() ?? "";
			}
			catch
			{
				instance = "";
			}

			return new CounterSettingsDto
			{
				Id = Guid.NewGuid(),
				Category = "PhysicalDisk",
				Counter = "% Disk Time",
				Instance = instance,
				DisplayName = "Disk Activity",
				Min = 0,
				Max = 100,
				ShowInTray = false,
				IconSet = "Activity"
			};
		}

		public SettingsOptions Create()
		{
			return new SettingsOptions(
				new List<CounterSettingsDto>
				{
				CreateDefaultCounter()
				},
				true,
				SettingsOptions.CurrentVersion
			);
		}
	}
}
