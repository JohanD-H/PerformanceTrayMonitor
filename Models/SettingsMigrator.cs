using PerformanceTrayMonitor.Configuration;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Xml.Serialization;

namespace PerformanceTrayMonitor.Models
{
	internal sealed class SettingsMigrator
	{
		private readonly DefaultSettingsProvider _defaults = new();

		// -------------------------------
		// MAIN ENTRY POINT
		// -------------------------------
		public SettingsOptions LoadOrMigrate(string filePath)
		{
			if (!File.Exists(filePath))
			{
				Log.Warning("Settings file not found. Using defaults.");
				return _defaults.Create();
			}

			// 1. Try versioned SettingsFile (v2)
			var v2 = TryLoadV2(filePath);
			if (v2 != null)
				return v2;

			// 2. Try old DTO list format
			var dtoList = TryLoadOldDtoList(filePath);
			if (dtoList != null)
			{
				NotifyUpgrade();
				return new SettingsOptions(dtoList, Version: 2);
			}

			// 3. Try old CounterSettings format
			var oldList = TryLoadOldCounterSettings(filePath);
			if (oldList != null)
			{
				NotifyUpgrade();
				return new SettingsOptions(oldList, Version: 2);
			}

			// 4. Everything failed → defaults
			Log.Error("All migration attempts failed. Using defaults.");
			return _defaults.Create();
		}

		// -------------------------------
		// LOAD VERSION 2 FORMAT
		// -------------------------------
		private SettingsOptions TryLoadV2(string filePath)
		{
			try
			{
				var serializer = new XmlSerializer(typeof(SettingsFile));
				using var fs = File.OpenRead(filePath);

				var file = (SettingsFile)serializer.Deserialize(fs);

				if (file.Version == 2)
				{
					Log.Debug("Loaded settings (version 2)");
					return new SettingsOptions(file.Counters, Version: 2);
				}

				Log.Warning($"Unknown settings version {file.Version}, attempting migration...");
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Failed to load versioned settings.");
			}

			return null;
		}

		// -------------------------------
		// LOAD OLD DTO LIST FORMAT
		// -------------------------------
		private List<CounterSettingsDto> TryLoadOldDtoList(string filePath)
		{
			try
			{
				var serializer = new XmlSerializer(typeof(List<CounterSettingsDto>));
				using var fs = File.OpenRead(filePath);

				return (List<CounterSettingsDto>)serializer.Deserialize(fs);
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Old DTO list migration failed.");
				return null;
			}
		}

		// -------------------------------
		// LOAD OLD CounterSettings FORMAT
		// -------------------------------
		private List<CounterSettingsDto> TryLoadOldCounterSettings(string filePath)
		{
			try
			{
				var serializer = new XmlSerializer(typeof(List<CounterSettings>));
				using var fs = File.OpenRead(filePath);

				var oldList = (List<CounterSettings>)serializer.Deserialize(fs);

				return oldList.Select(s => new CounterSettingsDto
				{
					Id = Guid.NewGuid(),
					Category = s.Category,
					Counter = s.Counter,
					Instance = s.Instance,
					DisplayName = s.DisplayName,
					Min = s.Min,
					Max = s.Max,
					Mode = s.Mode,
					ShowInTray = s.ShowInTray,
					IconSet = s.IconSet
				}).ToList();
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Old CounterSettings migration failed.");
				return null;
			}
		}

		// -------------------------------
		// UPGRADE NOTICE
		// -------------------------------
		private void NotifyUpgrade()
		{
			MessageBox.Show(
				"Your settings file was created by an older version of PerfLED.\n\n" +
				"It has been automatically upgraded to the new format.",
				"Settings Upgraded",
				MessageBoxButton.OK,
				MessageBoxImage.Information
			);
		}
	}
}
