using PerformanceTrayMonitor.Configuration;
using PerformanceTrayMonitor.Common;
using System;
using System.IO;
using System.Xml.Serialization;
using P = PerformanceTrayMonitor.Configuration.Paths;

namespace PerformanceTrayMonitor.Models
{
	public static class SettingsStore
	{
		private static readonly SettingsMigrator _migrator = new();
		private static readonly DefaultSettingsProvider _defaults = new();

		public static SettingsOptions Load()
		{
			Log.Debug($"Loading settings from: {P.SettingsFile}");
			return _migrator.LoadOrMigrate(P.SettingsFile);
		}

		public static void Save(SettingsOptions options)
		{
			if (options == null)
			{
				Log.Error("Attempted to save null SettingsOptions. Falling back to defaults.");
				options = _defaults.Create();
			}

			Log.Debug($"Saving settings to: {P.SettingsFile}");

			var file = new SettingsFile
			{
				Version = SettingsOptions.CurrentVersion,
				Counters = options.Counters,
				ShowAppIcon = options.ShowAppIcon
			};

			try
			{
				var serializer = new XmlSerializer(typeof(SettingsFile));
				using var fs = File.Create(P.SettingsFile);
				serializer.Serialize(fs, file);
			}
			catch (Exception ex)
			{
				Log.Error($"{ex} Failed to save settings file.");
			}
		}
	}
}
