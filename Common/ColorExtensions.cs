using System.Windows.Media;

namespace PerformanceTrayMonitor.Common
{
	public static class ColorExtensions
	{
		public static int ToArgb(this System.Windows.Media.Color c) =>
			(c.A << 24) | (c.R << 16) | (c.G << 8) | c.B;

		public static System.Windows.Media.Color FromArgb(int argb) =>
			System.Windows.Media.Color.FromArgb(
				(byte)(argb >> 24),
				(byte)(argb >> 16),
				(byte)(argb >> 8),
				(byte)argb
			);
	}
}
