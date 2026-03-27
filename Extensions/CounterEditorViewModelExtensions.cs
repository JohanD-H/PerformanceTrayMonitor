using PerformanceTrayMonitor.Models;
//using PerformanceTrayMonitor.Settings;
using PerformanceTrayMonitor.ViewModels;

namespace PerformanceTrayMonitor.Extensions
{
	public static class CounterEditorExtensions
	{
		public static CounterSettings ToSettings(this CounterEditorViewModel editor)
		{
			return new CounterSettings
			{
				Id = editor.Id, // Guid.Empty means “new metric”
				Category = editor.SelectedCategory,
				Counter = editor.SelectedCounter,
				Instance = editor.SelectedInstance,
				DisplayName = editor.DisplayName,
				Min = editor.Min,
				Max = editor.Max,
				ShowInTray = editor.ShowInTray,
				IconSet = editor.IconSet,
				UseTextTrayIcon = editor.UseTextTrayIcon,
				TrayAccentColor = editor.TrayAccentColor,
				AutoTrayBackground = editor.AutoTrayBackground,
				TrayBackgroundColor = editor.TrayBackgroundColor
			};
		}
	}
}
