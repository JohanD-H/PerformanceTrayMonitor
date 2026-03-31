using PerformanceTrayMonitor.Common;
using PerformanceTrayMonitor.Settings;
using PerformanceTrayMonitor.Models;
using System;
using System.Windows;

namespace PerformanceTrayMonitor.Extensions
{
	public static class CounterSettingsDtoExtensions
	{
		public static CounterSettings ToSettings(this CounterSettingsDto dto)
		{
			return new CounterSettings
			{
				Id = dto.Id,
				Category = dto.Category,
				Counter = dto.Counter,
				Instance = dto.Instance,
				DisplayName = dto.DisplayName,
				Min = dto.Min,
				Max = dto.Max,
				ShowInTray = dto.ShowInTray,
				IconSet = dto.IconSet,

				UseTextTrayIcon = dto.UseTextTrayIcon,
				TrayAccentColor = ColorExtensions.FromArgb(dto.TrayAccentColorArgb),
				AutoTrayBackground = dto.AutoTrayBackground,
				TrayBackgroundColor = ColorExtensions.FromArgb(dto.TrayBackgroundColorArgb),
			};
		}
	}
}
