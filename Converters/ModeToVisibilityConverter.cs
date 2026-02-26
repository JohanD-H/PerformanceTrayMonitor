using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Serilog;

namespace PerformanceTrayMonitor.Converters
{
	public class ModeToVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			string mode = value?.ToString() ?? "";
			string target = parameter?.ToString() ?? "";
			Log.Debug($"mode = {mode}");

			return mode.Equals(target, StringComparison.OrdinalIgnoreCase)
				? Visibility.Visible
				: Visibility.Collapsed;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> throw new NotImplementedException();
	}
}