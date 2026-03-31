using System;
using System.Windows;
using System.Globalization;
using System.Windows.Data;

namespace PerformanceTrayMonitor.Converters
{
	public class BoolToOpacityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is bool b)
				return b ? 1.0 : 0.0;

			return 0.0;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
