using PerformanceTrayMonitor.Common;
using PerformanceTrayMonitor.Settings;
using PerformanceTrayMonitor.ViewModels;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace PerformanceTrayMonitor.Models
{
	public static class SettingsMapper
	{
		// ---------------------------------------------------------
		// DTO -> Runtime
		// ---------------------------------------------------------
		public static SettingsOptions ToRuntime(SettingsDto dto)
		{
			return new SettingsOptions
			{
				Global = new GlobalOptions
				{
					ShowAppIcon = dto.Global.ShowAppIcon,
					PopupPinned = dto.Global.PopupPinned,
					PopupMonitorId = dto.Global.PopupMonitorId,
					PopupX = dto.Global.PopupX,
					PopupY = dto.Global.PopupY,
					PopupDpi = dto.Global.PopupDpi,
					PopupWasOpen = dto.Global.PopupWasOpen,
					CustomColors = dto.Global.CustomColors.ToArray()
				},

				Metrics = dto.Metrics.Select(m => new CounterSettings
				{
					Id = m.Id, // ID comes from file, not generated

					Category = m.Category,
					Counter = m.Counter,
					Instance = m.Instance,
					DisplayName = m.DisplayName,

					Min = m.Min,
					Max = m.Max,
					ShowInTray = m.ShowInTray,
					IconSet = m.IconSet,

					UseTextTrayIcon = m.UseTextTrayIcon,

					TrayAccentColor = Color.FromArgb(
						(byte)((m.TrayAccentColorArgb >> 24) & 0xFF),
						(byte)((m.TrayAccentColorArgb >> 16) & 0xFF),
						(byte)((m.TrayAccentColorArgb >> 8) & 0xFF),
						(byte)(m.TrayAccentColorArgb & 0xFF)
					),

					AutoTrayBackground = m.AutoTrayBackground,

					TrayBackgroundColor = Color.FromArgb(
						(byte)((m.TrayBackgroundColorArgb >> 24) & 0xFF),
						(byte)((m.TrayBackgroundColorArgb >> 16) & 0xFF),
						(byte)((m.TrayBackgroundColorArgb >> 8) & 0xFF),
						(byte)(m.TrayBackgroundColorArgb & 0xFF)
					)
				}).ToList()
			};
		}


		// ---------------------------------------------------------
		// Runtime -> DTO
		// ---------------------------------------------------------
		public static SettingsDto ToDto(SettingsOptions options)
		{
			return new SettingsDto
			{
				Version = SettingsDto.CurrentVersion,

				Global = new GlobalOptionsDto
				{
					ShowAppIcon = options.Global.ShowAppIcon,
					PopupPinned = options.Global.PopupPinned,
					PopupMonitorId = options.Global.PopupMonitorId,
					PopupX = options.Global.PopupX,
					PopupY = options.Global.PopupY,
					PopupDpi = options.Global.PopupDpi,
					PopupWasOpen = options.Global.PopupWasOpen,
					CustomColors = options.Global.CustomColors.ToArray(),
				},

				Metrics = options.Metrics.Select(m => new CounterSettingsDto
				{
					Id = m.Id, // preserve identity

					Category = m.Category,
					Counter = m.Counter,
					Instance = m.Instance,
					DisplayName = m.DisplayName,

					Min = m.Min,
					Max = m.Max,
					ShowInTray = m.ShowInTray,
					IconSet = m.IconSet,

					UseTextTrayIcon = m.UseTextTrayIcon,

					TrayAccentColorArgb = m.TrayAccentColor.ToArgb(),
					AutoTrayBackground = m.AutoTrayBackground,
					TrayBackgroundColorArgb = m.TrayBackgroundColor.ToArgb()
				}).ToList()
			};
		}

		public static CounterSettingsDto ToCounterDto(CounterSettings m)
		{
			return new CounterSettingsDto
			{
				Id = m.Id,
				Category = m.Category,
				Counter = m.Counter,
				Instance = m.Instance,
				DisplayName = m.DisplayName,
				Min = m.Min,
				Max = m.Max,
				ShowInTray = m.ShowInTray,
				IconSet = m.IconSet,
				UseTextTrayIcon = m.UseTextTrayIcon,
				TrayAccentColorArgb = m.TrayAccentColor.ToArgb(),
				AutoTrayBackground = m.AutoTrayBackground,
				TrayBackgroundColorArgb = m.TrayBackgroundColor.ToArgb()
			};
		}

		// ---------------------------------------------------------
		// DTO -> Runtime
		// ---------------------------------------------------------
		public static SettingsOptions ToOptions(SettingsDto dto)
		{
			// If no file exists or load failed, return defaults
			if (dto == null)
				return new SettingsOptions();

			var options = new SettingsOptions
			{
				Global = new GlobalOptions
				{
					ShowAppIcon = dto.Global.ShowAppIcon,
					PopupPinned = dto.Global.PopupPinned,
					PopupMonitorId = dto.Global.PopupMonitorId,
					PopupX = dto.Global.PopupX,
					PopupY = dto.Global.PopupY,
					PopupDpi = dto.Global.PopupDpi,
					PopupWasOpen = dto.Global.PopupWasOpen,
					CustomColors = dto.Global.CustomColors?.ToArray() ?? new int[16]
				},

				Metrics = dto.Metrics.Select(m => new CounterSettings
				{
					Id = m.Id,

					Category = m.Category,
					Counter = m.Counter,
					Instance = m.Instance,
					DisplayName = m.DisplayName,

					Min = m.Min,
					Max = m.Max,
					ShowInTray = m.ShowInTray,
					IconSet = m.IconSet,

					UseTextTrayIcon = m.UseTextTrayIcon,

					TrayAccentColor = FromArgbInt(m.TrayAccentColorArgb),
					AutoTrayBackground = m.AutoTrayBackground,
					TrayBackgroundColor = FromArgbInt(m.TrayBackgroundColorArgb),

				}).ToList()
			};

			return options;
		}

		public static SettingsOptions FromViewModel(ConfigViewModel vm)
		{
			if (vm == null)
				throw new ArgumentNullException(nameof(vm));

			return new SettingsOptions
			{
				// Global settings are not edited in the Config window,
				// so we reuse the existing GlobalOptions object.
				Global = vm.GlobalSettings.Global,

				// Metrics come directly from the CounterViewModels.
				Metrics = vm.Metrics
					.Select(m => new CounterSettings
					{
						Id = m.Settings.Id,
						Category = m.Settings.Category,
						Counter = m.Settings.Counter,
						Instance = m.Settings.Instance,
						DisplayName = m.Settings.DisplayName,
						Min = m.Settings.Min,
						Max = m.Settings.Max,
						ShowInTray = m.Settings.ShowInTray,
						IconSet = m.Settings.IconSet,
						UseTextTrayIcon = m.Settings.UseTextTrayIcon,
						TrayAccentColor = m.Settings.TrayAccentColor,
						AutoTrayBackground = m.Settings.AutoTrayBackground,
						TrayBackgroundColor = m.Settings.TrayBackgroundColor
					})
					.ToList()
			};
		}

		private static System.Windows.Media.Color FromArgbInt(int argb)
		{
			return System.Windows.Media.Color.FromArgb(
				(byte)((argb >> 24) & 0xFF),
				(byte)((argb >> 16) & 0xFF),
				(byte)((argb >> 8) & 0xFF),
				(byte)(argb & 0xFF)
			);
		}

	}
}
