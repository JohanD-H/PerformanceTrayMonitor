using PerformanceTrayMonitor.Configuration;
using Serilog;
using System.Collections.Generic;
using P = PerformanceTrayMonitor.Configuration.Paths;


namespace PerformanceTrayMonitor.Models
{
	/// <summary>
	/// Thin façade over the SettingsMigrator + SettingsProvider architecture.
	/// Handles only: load, save, and delegating to the correct components.
	/// </summary>
	public static class SettingsStore
	{
		private static readonly SettingsMigrator _migrator = new();
		private static readonly DefaultSettingsProvider _defaults = new();

		// -------------------------------
		// LOAD (with migration)
		// -------------------------------
		public static List<CounterSettingsDto> Load()
		{
			Log.Debug($"Loading settings from: {P.SettingsFile}");

			// Let the migrator handle:
			// - Missing file
			// - Versioned XML
			// - Old DTO list
			// - Old CounterSettings
			// - Corruption fallback
			var options = _migrator.LoadOrMigrate(P.SettingsFile);

			return options.Counters;
		}

		// -------------------------------
		// SAVE (always version 2)
		// -------------------------------
		public static void Save(List<CounterSettingsDto> settings)
		{
			Log.Debug($"Saving settings to: {P.SettingsFile}");

			var file = new SettingsFile
			{
				Version = 2,
				Counters = settings
			};

			var serializer = new System.Xml.Serialization.XmlSerializer(typeof(SettingsFile));
			using var fs = System.IO.File.Create(P.SettingsFile);
			serializer.Serialize(fs, file);
		}
	}
}
