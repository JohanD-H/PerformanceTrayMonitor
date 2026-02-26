using PerformanceTrayMonitor.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace PerformanceTrayMonitor.Configuration
{
	internal sealed record SettingsOptions(
		List<CounterSettingsDto> Counters,
		int Version
	);

	internal interface ISettingsProvider
	{
		SettingsOptions Load();
		void Save(SettingsOptions options);
	}

}

