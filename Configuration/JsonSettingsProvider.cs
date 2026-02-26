using PerformanceTrayMonitor.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PerformanceTrayMonitor.Configuration
{
	internal sealed class JsonSettingsProvider : ISettingsProvider
	{
		public SettingsOptions Load()
		{
			if (!File.Exists(Paths.SettingsFile))
				return CreateDefault();

			var json = File.ReadAllText(Paths.SettingsFile);

			return JsonSerializer.Deserialize<SettingsOptions>(json)
				   ?? CreateDefault();
		}

		public void Save(SettingsOptions options)
		{
			var json = JsonSerializer.Serialize(options, new JsonSerializerOptions
			{
				WriteIndented = true
			});

			File.WriteAllText(Paths.SettingsFile, json);
		}

		private SettingsOptions CreateDefault()
		{
			return new SettingsOptions(
				Counters: new List<CounterSettingsDto>
				{
					// your default counter(s)
				},
				Version: 1
			);
		}
	}
}
