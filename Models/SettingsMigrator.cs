using PerformanceTrayMonitor.Configuration;
using PerformanceTrayMonitor.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Xml.Serialization;

// ---------------------------------------
// Converting previous settings to current
// ---------------------------------------
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

			// Try versioned SettingsFile (v2)
			var v2 = TryLoadV2(filePath);
			if (v2 != null)
				return v2;

			// Try old DTO list format
			var dtoList = TryLoadOldDtoList(filePath);
			if (dtoList != null)
			{
				NotifyUpgrade();
				return new SettingsOptions(dtoList, true, SettingsOptions.CurrentVersion);
			}

			// Try old CounterSettings format
			var oldList = TryLoadOldCounterSettings(filePath);
			if (oldList != null)
			{
				NotifyUpgrade();
				return new SettingsOptions(oldList, true, SettingsOptions.CurrentVersion);

			}

			// Everything failed → defaults
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

				// -------------------------------
				// VERSION 3 (current)
				// -------------------------------
				if (file.Version == SettingsOptions.CurrentVersion)
				{
					Log.Debug($"Loaded settings (version {SettingsOptions.CurrentVersion})");

					file.Counters ??= new List<CounterSettingsDto>();

					Log.Debug($"Loaded Counters = {file.Counters}");
					Log.Debug($"Loaded ShowAppIcon = {file.ShowAppIcon}");
					Log.Debug($"Loaded PopupPinned = {file.PopupPinned}");
					Log.Debug($"Loaded PopupMonitorId = {file.PopupMonitorId}");
					Log.Debug($"Loaded PopupX = {file.PopupX}");
					Log.Debug($"Loaded PopupY = {file.PopupY}");
					Log.Debug($"Loaded PopupDpi = {file.PopupDpi}");
					Log.Debug($"Loaded PopupWasOpen = {file.PopupWasOpen}");

					return new SettingsOptions(
						file.Counters,
						file.ShowAppIcon,
						SettingsOptions.CurrentVersion
					)
					{
						PopupPinned = file.PopupPinned,
						PopupMonitorId = file.PopupMonitorId,
						PopupX = file.PopupX,
						PopupY = file.PopupY,
						PopupDpi = file.PopupDpi,
						PopupWasOpen = file.PopupWasOpen
					};
				}

				// -------------------------------
				// VERSION 2 → migrate to version 3
				// -------------------------------
				if (file.Version == 2)
				{
					Log.Debug("Migrating settings from v2 to v3");

					file.Counters ??= new List<CounterSettingsDto>();

					return new SettingsOptions(
						file.Counters,
						file.ShowAppIcon,
						3
					)
					{
						PopupPinned = false,
						PopupMonitorId = null,
						PopupX = null,
						PopupY = null,
						PopupDpi = null,
						PopupWasOpen = false
					};
				}

				// -------------------------------
				// Unknown version
				// -------------------------------
				Log.Warning($"Unknown settings version {file.Version}, attempting migration...");
			}
			catch (Exception ex)
			{
				Log.Warning($"{ex} Failed to load versioned settings.");
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
				Log.Warning($"{ex} Old DTO list migration failed.");
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
					ShowInTray = s.ShowInTray,
					IconSet = s.IconSet
				}).ToList();
			}
			catch (Exception ex)
			{
				Log.Error($"{ex} Old CounterSettings migration failed.");
				return null;
			}
		}

		// -------------------------------
		// UPGRADE NOTICE
		// -------------------------------
		private void NotifyUpgrade()
		{
			MessageBox.Show(
				"Your settings file was created by an older version of PerformanceTrayMonitor.\n\n" +
				"It has been automatically upgraded to the new format.",
				"Settings Upgraded",
				MessageBoxButton.OK,
				MessageBoxImage.Information
			);
		}
	}
}
