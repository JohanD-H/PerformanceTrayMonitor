using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Serilog;

// ---------------------------------------
// Brush convertor....
// ---------------------------------------
namespace PerformanceTrayMonitor.Converters
{
	public class ActivityToBrushConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is float v)
			{
				Log.Debug($"Value = {v}");
				if (v > 50) return Brushes.Red;
				if (v > 20) return Brushes.Orange;
				if (v > 5) return Brushes.Yellow;
				return Brushes.Gray;
			}

			return Brushes.Gray;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> throw new NotImplementedException();
	}
}