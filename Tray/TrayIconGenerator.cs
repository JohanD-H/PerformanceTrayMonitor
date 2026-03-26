using System;
using System.Drawing;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace PerformanceTrayMonitor.Tray
{
	public static class TrayIconGenerator
	{
		[DllImport("user32.dll", CharSet = CharSet.Auto)]
		private static extern bool DestroyIcon(IntPtr handle);

		private static Icon CreateIconSafe(Bitmap bmp)
		{
			IntPtr hIcon = bmp.GetHicon();

			// Clone to detach from the unmanaged handle
			Icon icon = (Icon)Icon.FromHandle(hIcon).Clone();

			// Prevent GDI handle leak
			DestroyIcon(hIcon);

			return icon;
		}

		/// <summary>
		/// Creates a small text-based tray icon.
		/// </summary>
		/// <param name="text">Text to render (keep it short: 2–3 chars).</param>
		/// <param name="foreground">Text color.</param>
		/// <param name="background">Background color.</param>
		/// <param name="dpiScale">DPI scale factor (1.0 for 100%, 1.25 for 125%, etc.).</param>
		public static Icon CreateTextIcon(
			string text,
			Color foreground,
			Color background,
			double dpiScale = 1.0)
		{
			if (string.IsNullOrWhiteSpace(text))
				text = "?";

			int size = (int)Math.Round(16 * dpiScale); // 16x16 base, scaled for DPI
			if (size < 16)
				size = 16;

			using var bmp = new Bitmap(size, size);
			using var g = Graphics.FromImage(bmp);

			g.Clear(background);
			g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;

			// Font size tuned for tiny icons; scaled with DPI
			float fontSize = (float)(8.0 * dpiScale);
			using var font = new System.Drawing.Font("Segoe UI", fontSize, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel);
			using var brush = new SolidBrush(foreground);

			var rect = new RectangleF(0, 0, size, size);
			var sf = new StringFormat
			{
				Alignment = StringAlignment.Center,
				LineAlignment = StringAlignment.Center,
				FormatFlags = StringFormatFlags.NoWrap
			};

			g.DrawString(text, font, brush, rect, sf);

			return CreateIconSafe(bmp);
		}

		public static BitmapSource CreateTextBitmapSource(
			string text,
			System.Drawing.Color foreground,
			System.Drawing.Color background,
			double dpiScale = 1.0)
		{
			if (string.IsNullOrWhiteSpace(text))
				text = "?";

			int size = (int)Math.Round(16 * dpiScale);
			if (size < 16)
				size = 16;

			using var bmp = new Bitmap(size, size);
			using var g = Graphics.FromImage(bmp);

			g.Clear(background);
			g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;

			float fontSize = (float)(8.0 * dpiScale);
			using var font = new System.Drawing.Font("Segoe UI", fontSize, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel);
			using var brush = new SolidBrush(foreground);

			var rect = new RectangleF(0, 0, size, size);
			var sf = new StringFormat
			{
				Alignment = StringAlignment.Center,
				LineAlignment = StringAlignment.Center,
				FormatFlags = StringFormatFlags.NoWrap
			};

			g.DrawString(text, font, brush, rect, sf);

			// Convert GDI+ Bitmap → WPF BitmapSource
			var hBitmap = bmp.GetHbitmap();

			try
			{
				return Imaging.CreateBitmapSourceFromHBitmap(
					hBitmap,
					IntPtr.Zero,
					Int32Rect.Empty,
					BitmapSizeOptions.FromWidthAndHeight(size, size));
			}
			finally
			{
				// Prevent memory leaks
				DeleteObject(hBitmap);
			}
		}

		[DllImport("gdi32.dll")]
		private static extern bool DeleteObject(IntPtr hObject);

		public static int GetFrameIndex(double value, double min, double max, int frameCount)
		{
			// If there is only 1 frame (icon) don't bother to calculate!
			if (frameCount <= 1)
				return 0;

			// Don't want an accidental devision by 0 when normalizing!
			if (max <= min)
				return 0; // always show first frame

			double val = Math.Max(min, Math.Min(max, value));
			//Log.Debug($"val = {val}");

			double normalized = (val - min) / (max - min);
			//Log.Debug($"normalized = {normalized}");

			// Below both work, but pick what feels best.
			//
			// Normalize, standard
			// int index = (int)(normalized * (frameCount - 1));
			// Normalize, but gives a smoother transition
			int index = (int)Math.Round(normalized * (frameCount - 1));

			return Math.Max(0, Math.Min(frameCount - 1, index));
		}
	}
}
