using System;
using System.Windows;
using System.Collections.Generic;

namespace PerformanceTrayMonitor.Settings
{
	public sealed class SettingsDto
	{
		public const int CurrentVersion = 1;

		public int Version { get; set; }

		public GlobalOptionsDto Global { get; set; } = new();
		public List<CounterSettingsDto> Metrics { get; set; } = new();
	}
}
