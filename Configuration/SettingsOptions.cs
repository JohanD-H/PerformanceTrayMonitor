using PerformanceTrayMonitor.Models;
using System;
using System.Collections.Generic;
using System.IO;

// ------------------------------------------
// Options
// ------------------------------------------
namespace PerformanceTrayMonitor.Configuration
{
	public sealed record SettingsOptions
	{
		public const int CurrentVersion = 2;

		public List<CounterSettingsDto> Counters { get; set; } = new();
		public bool ShowAppIcon { get; set; } = true;
		public int Version { get; set; } = CurrentVersion;

		public SettingsOptions() { }

		public SettingsOptions(List<CounterSettingsDto> counters, bool showAppIcon)
			: this(counters, showAppIcon, CurrentVersion)
		{
		}

		public SettingsOptions(List<CounterSettingsDto> counters, bool showAppIcon, int version)
		{
			Counters = counters ?? new List<CounterSettingsDto>();
			ShowAppIcon = showAppIcon;
			Version = version;
		}
	}

	internal interface ISettingsProvider
	{
		SettingsOptions Load();
		void Save(SettingsOptions options);
	}

}

