using PerformanceTrayMonitor.Models;
using PerformanceTrayMonitor.ViewModels;
using System;
using System.Windows;

namespace PerformanceTrayMonitor.Extensions
{
	internal static class CounterViewModelExtensions
	{
		public static CounterSettings ToSettings(this CounterViewModel vm)
		{
			return new CounterSettings
			{
				Id = vm.Id,
				DisplayName = vm.DisplayName,
				Category = vm.Category,
				Counter = vm.Counter,
				Instance = vm.Instance,
				Min = vm.Min,
				Max = vm.Max,
				ShowInTray = vm.ShowInTray,
				IconSet = vm.IconSet,
				UseTextTrayIcon = vm.UseTextTrayIcon,
				TrayAccentColor = vm.TrayAccentColor,
				AutoTrayBackground = vm.AutoTrayBackground,
				TrayBackgroundColor = vm.TrayBackgroundColor
			};
		}
	}
}
