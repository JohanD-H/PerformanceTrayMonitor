using PerformanceTrayMonitor.ViewModels;
using Serilog;
using System;
using System.Windows;
using System.Windows.Media.Animation;

// --------------------------------
// Configuration window
// --------------------------------
namespace PerformanceTrayMonitor.Views
{
	public partial class ConfigWindow : Window
	{
		public ConfigWindow(ConfigViewModel vm)
		{
			InitializeComponent();

			DataContext = vm;

			Log.Debug("ConfigWindow DataContext = " + DataContext?.GetType().Name);

			Loaded += (s, e) =>
			{
				// Select first item if available
				if (vm.Counters.Count > 0 && vm.Selected == null)
					vm.Selected = vm.Counters[0];
			};

			vm.RequestClose += () => this.Close();
		}

		private void ButtonFlashHandler(object sender, RoutedEventArgs e)
		{
			var sb = (Storyboard)FindResource("ButtonFlashStoryboard");
			//sb.Begin((Button)sender);
			sb.Begin((FrameworkElement)sender);

		}

		private void ApplyButton_Click(object sender, RoutedEventArgs e)
		{
			var sb = (Storyboard)FindResource("PanelPulseStoryboard");
			sb.Begin(DetailsPanel);
		}

	}
}
