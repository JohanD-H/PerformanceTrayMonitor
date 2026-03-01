using PerformanceTrayMonitor.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Serilog;

// -------------------------------------------
// Loading/Saving data
// -------------------------------------------
namespace PerformanceTrayMonitor.Configuration
{
	internal sealed class JsonSettingsProvider : ISettingsProvider
	{
		public SettingsOptions Load()
		{
			if (!File.Exists(Paths.SettingsFile))
				return CreateDefault();

			var json = File.ReadAllText(Paths.SettingsFile);

			try
			{
				return JsonSerializer.Deserialize<SettingsOptions>(json) ?? CreateDefault();
			}
			catch
			{
				return CreateDefault();
			}
		}

		public void Save(SettingsOptions options)
		{
			var json = JsonSerializer.Serialize(options, new JsonSerializerOptions
			{
				WriteIndented = true
			});

			try
			{
				File.WriteAllText(Paths.SettingsFile, json);
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Failed to save JSON settings.");
			}
		}

		private SettingsOptions CreateDefault()
		{
			return new DefaultSettingsProvider().Create();
		}
	}
}
