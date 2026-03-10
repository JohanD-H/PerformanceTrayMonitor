using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using MediaColor = System.Windows.Media.Color;

namespace PerformanceTrayMonitor.Views
{
	public partial class AboutWindow : Window
	{
		private readonly List<float> _ambientValues = new();
		private System.Windows.Threading.DispatcherTimer _timer;

		public AboutWindow()
		{
			InitializeComponent();
			Loaded += AboutWindow_Loaded;
		}

		private void AboutWindow_Loaded(object sender, RoutedEventArgs e)
		{
			var version = Assembly.GetExecutingAssembly().GetName().Version;
			VersionTextBlock.Text = $"Version {version.Major}.{version.Minor}.{version.Build}";

			// Seed initial values
			_ambientValues.Clear();
			_ambientValues.AddRange(GenerateAmbientValues(60));

			// Give the title some color
			var (brush, _) = GetSoftColorFor("Performance Tray Monitor");
			TitleText.Foreground = brush;

			BackgroundSparkline.Min = 0;
			BackgroundSparkline.Max = 100;
			BackgroundSparkline.Values = new List<float>(_ambientValues);

			// Start a gentle scrolling timer
			_timer = new System.Windows.Threading.DispatcherTimer
			{
				Interval = TimeSpan.FromMilliseconds(250)
			};
			_timer.Tick += Timer_Tick;
			_timer.Start();
		}

		private List<float> GenerateAmbientValues(int count)
		{
			var list = new List<float>(count);
			var rand = new Random();

			// Smooth pseudo‑random curve
			float current = 50f;

			for (int i = 0; i < count; i++)
			{
				// Gentle drift
				current += (float)(rand.NextDouble() * 10 - 5);

				// Clamp to range
				if (current < 10) current = 10;
				if (current > 90) current = 90;

				list.Add(current);
			}

			return list;
		}

		private void OkButton_Click(object sender, RoutedEventArgs e)
		{
			_timer?.Stop();
			Close();
		}

		private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (e.ButtonState == MouseButtonState.Pressed)
				DragMove();
		}

		private void Timer_Tick(object? sender, EventArgs e)
		{
			if (_ambientValues.Count == 0)
				return;

			_ambientValues.RemoveAt(0);

			var rand = new Random();
			float last = _ambientValues[^1];
			last += (float)(rand.NextDouble() * 10 - 5);
			if (last < 10) last = 10;
			if (last > 90) last = 90;
			_ambientValues.Add(last);

			BackgroundSparkline.Values = new List<float>(_ambientValues);
		}

		private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
		{
			Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
			e.Handled = true;
		}

		protected override void OnContentRendered(EventArgs e)
		{
			base.OnContentRendered(e);

			var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
			var duration = TimeSpan.FromMilliseconds(180);

			// Fade-in
			BeginAnimation(Window.OpacityProperty,
				new DoubleAnimation(0, 1, duration) { EasingFunction = ease });

			// Scale-in
			Root.RenderTransform.BeginAnimation(
				ScaleTransform.ScaleXProperty,
				new DoubleAnimation(0.98, 1, duration) { EasingFunction = ease });

			Root.RenderTransform.BeginAnimation(
				ScaleTransform.ScaleYProperty,
				new DoubleAnimation(0.98, 1, duration) { EasingFunction = ease });
		}

		protected override void OnPreviewKeyDown(KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				Close();
				e.Handled = true;
			}

			base.OnPreviewKeyDown(e);
		}

		// With luminance compensation, colors in a UI are hard....
		private (SolidColorBrush Brush, double ShadowOpacity) GetSoftColorFor(string name)
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

		private MediaColor HslToColor(double h, double s, double l)
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

		private double HueToRgb(double t1, double t2, double hue)
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
