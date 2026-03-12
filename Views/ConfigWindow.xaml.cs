using PerformanceTrayMonitor.Common;
using PerformanceTrayMonitor.ViewModels;
using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace PerformanceTrayMonitor.Views
{
	public partial class ConfigWindow : Window
	{
		public ConfigWindow(ConfigViewModel vm)
		{
			InitializeComponent();
			this.DataContext = vm;
			vm.RequestClose += () => this.Close();
		}

		private void ButtonFlashHandler(object sender, RoutedEventArgs e)
		{
			if (FindResource("ButtonFlashStoryboard") is Storyboard sb)
				sb.Begin((FrameworkElement)sender);
		}
		private void ApplyButton_Click(object sender, RoutedEventArgs e)
		{
			if (FindResource("PanelPulseStoryboard") is System.Windows.Media.Animation.Storyboard sb)
			{
				sb.Begin(DetailsPanel);
			}
		}
	}
}
