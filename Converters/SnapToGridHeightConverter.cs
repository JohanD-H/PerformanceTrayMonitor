using System;
using System.Windows;
using System.Globalization;
using System.Windows.Data;

namespace PerformanceTrayMonitor.Converters
{
	public class SnapToGridHeightConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is double h)
			{
				double cell = 12.0;

				double snapped = Math.Floor(h / cell) * cell;

				// Prevent SparkLine from collapsing to zero height
				if (snapped < 48)
					snapped = 48;

				return snapped;
			}

			return value;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
