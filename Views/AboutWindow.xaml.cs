using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;

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

	}
}
