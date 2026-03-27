using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using PerformanceTrayMonitor.Common;

namespace PerformanceTrayMonitor.Views
{
	public partial class AboutWindow : Window
	{
		private readonly List<float> _ambientValues = new();
		private System.Windows.Threading.DispatcherTimer _timer;
		public ICommand CloseAboutWindow { get; }

		public AboutWindow()
		{
			CloseAboutWindow = new RelayCommand(_ => Close());

			InitializeComponent();
			Loaded += AboutWindow_Loaded;
			Loaded += (_, __) =>
			{
				Log.Debug($"AboutWindow IsEnabled = {IsEnabled}");
			};
		}

		private void AboutWindow_Loaded(object sender, RoutedEventArgs e)
		{
			var version = Assembly.GetExecutingAssembly().GetName().Version;
			VersionTextBlock.Text = $"Version {version.Major}.{version.Minor}.{version.Build}";

			// Seed initial values
			_ambientValues.Clear();
			_ambientValues.AddRange(GenerateAmbientValues(60));

			// Give the title some color
			var (brush, _) = UIColors.GetSoftColorFor("Performance Tray Monitor");
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
	}
}
