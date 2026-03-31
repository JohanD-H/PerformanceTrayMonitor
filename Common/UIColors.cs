using System;
using System.Windows;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;

namespace PerformanceTrayMonitor.Common
{
	public static class UIColors
	{
		// Convert WPF Color → System.Drawing.Color
		public static System.Drawing.Color ToDrawingColor(this MediaColor c)
		{
			return System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
		}

		// Convert System.Drawing.Color → WPF Color
		public static MediaColor ToMediaColor(this System.Drawing.Color c)
		{
			return MediaColor.FromArgb(c.A, c.R, c.G, c.B);
		}

		// Auto-contrast: returns black or white depending on luminance
		public static System.Drawing.Color AutoContrast(System.Drawing.Color baseColor)
		{
			double luminance =
				(0.299 * baseColor.R +
				 0.587 * baseColor.G +
				 0.114 * baseColor.B) / 255.0;

			return luminance > 0.5
				? System.Drawing.Color.FromArgb(255, 0, 0, 0)   // black
				: System.Drawing.Color.FromArgb(255, 255, 255, 255); // white
		}

		// Lighten/darken a color by factor (0.0–1.0)
		public static System.Drawing.Color AdjustBrightness(System.Drawing.Color c, double factor)
		{
			int r = (int)(c.R * factor);
			int g = (int)(c.G * factor);
			int b = (int)(c.B * factor);

			return System.Drawing.Color.FromArgb(c.A,
				Math.Clamp(r, 0, 255),
				Math.Clamp(g, 0, 255),
				Math.Clamp(b, 0, 255));
		}

		// Generate a tray background color based on accent color
		public static System.Drawing.Color GetTrayBackground(System.Drawing.Color accent, bool autoContrast = true)
		{
			if (autoContrast)
				return AutoContrast(accent);

			// Default: slightly darkened version of accent
			return AdjustBrightness(accent, 0.25);
		}

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
