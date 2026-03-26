using PerformanceTrayMonitor.Configuration;
using System;
using System.Collections.Generic;
using System.Windows;

namespace PerformanceTrayMonitor.Models
{
	public sealed class SettingsOptions
	{
		public GlobalOptions Global { get; set; } = new();

		public List<CounterSettings> Metrics { get; set; } = new();

		public static SettingsOptions CreateDefault()
		{
			return new DefaultSettingsProvider().Create();
		}
	}
}
