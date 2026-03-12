using System;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;

namespace PerformanceTrayMonitor.Common
{
	public static class UIColors
	{
		// With luminance compensation, colors in a UI are hard....
		public static  (SolidColorBrush Brush, double ShadowOpacity) GetSoftColorFor(string name)
		{
			int hash = name.GetHashCode();
			double hue = (hash & 0xFFFFFF) % 360;
			double saturation = 0.80;


			double hueNorm = hue / 360.0;
			double compensation =
				0.10 * Math.Sin((hueNorm * 2 * Math.PI) - Math.PI / 2);

			// Boost saturation slightly for green/mint hues (120–160°)
			if (hue >= 120 && hue <= 160)
			{
				saturation = Math.Min(1.0, saturation + 0.15);
			}
			// Boost saturation slightly for blue/violet hues (240–280°)
			if (hue >= 240 && hue <= 280)
			{
				saturation = Math.Min(1.0, saturation + 0.15);
			}

			double lightness = 0.20 + compensation;
			lightness = Math.Clamp(lightness, 0.40, 0.60);

			double shadowOpacity = 0.25 + (0.20 * (0.60 - lightness));

			var c = HslToColor(hue, saturation, lightness);
			return (new SolidColorBrush(c), shadowOpacity);
		}

		public static MediaColor HslToColor(double h, double s, double l)
		{
			h /= 360.0;

			double r = l, g = l, b = l;
			if (s != 0)
			{
				double temp2 = (l < 0.5) ? l * (1.0 + s) : (l + s) - (l * s);
				double temp1 = 2.0 * l - temp2;

				r = HueToRgb(temp1, temp2, h + 1.0 / 3.0);
				g = HueToRgb(temp1, temp2, h);
				b = HueToRgb(temp1, temp2, h - 1.0 / 3.0);
			}

			return MediaColor.FromRgb((byte)(255 * r), (byte)(255 * g), (byte)(255 * b));
		}

		public static double HueToRgb(double t1, double t2, double hue)
		{
			if (hue < 0) hue += 1;
			if (hue > 1) hue -= 1;

			if (6 * hue < 1) return t1 + (t2 - t1) * 6 * hue;
			if (2 * hue < 1) return t2;
			if (3 * hue < 2) return t1 + (t2 - t1) * ((2.0 / 3.0) - hue) * 6;

			return t1;
		}
	}
}
