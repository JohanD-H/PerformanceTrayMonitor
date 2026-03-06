using System;
using System.Globalization;
using System.Windows.Data;

namespace PerformanceTrayMonitor.Converters
{
	public class ValueToSizeConverter : IMultiValueConverter
	{
		public double MinSize { get; set; } = 6;
		public double MaxSize { get; set; } = 14;

		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			if (values.Length < 3)
				return MinSize;

			if (values[0] is double current &&
				values[1] is double min &&
				values[2] is double max)
			{
				double range = max - min;
				if (range <= 0) return MinSize;

				double t = (current - min) / range;
				t = Math.Clamp(t, 0, 1);

				return MinSize + (MaxSize - MinSize) * t;
			}

			return MinSize;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
			=> throw new NotImplementedException();
	}
}
