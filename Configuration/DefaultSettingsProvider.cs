using PerformanceTrayMonitor.Models;
using PerformanceTrayMonitor.Settings;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Media;

namespace PerformanceTrayMonitor.Configuration
{
	internal sealed class DefaultSettingsProvider
	{
		public CounterSettings CreateDefaultCounter()
		{
			return new CounterSettings
			{
				Id = Guid.NewGuid(),
				Category = "PhysicalDisk",
				Counter = "% Disk Time",
				Instance = "_Total",
				DisplayName = "Disk Activity",
				Min = 0,
				Max = 100,
				ShowInTray = false,
				IconSet = "Activity",
				UseTextTrayIcon = false,
				TrayAccentColor = Colors.White,
				AutoTrayBackground = true,
				TrayBackgroundColor = Colors.Black
			};
		}
		/*
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
				IconSet = "Activity",
				UseTextTrayIcon = false,
				TrayAccentColorArgb = unchecked((int)0xFFFFFFFF),   // White
				AutoTrayBackground = true,
				TrayBackgroundColorArgb = unchecked((int)0xFF000000) // Black
			};
		}
		*/

		public SettingsOptions Create()
		{
			return new SettingsOptions
			{
				Global = new GlobalOptions
				{
					ShowAppIcon = true
				},
				Metrics = new List<CounterSettings>
				{
					CreateDefaultCounter()
				}
			};
		}
	}
}
