using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PerformanceTrayMonitor.Converters
{
	public class BoolToFontWeightConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return (value is bool b && b) ? FontWeights.Bold : FontWeights.Normal;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
