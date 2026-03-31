using PerformanceTrayMonitor.Models;
using System;
using System.Windows;
using System.Collections.Generic;
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
