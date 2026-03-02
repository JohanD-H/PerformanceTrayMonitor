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

			/* Added in ConfigWindow_Loaded
			Loaded += (s, e) =>
			{
				// Select first item if available
				if (vm.Counters.Count > 0 && vm.Selected == null)
					vm.Selected = vm.Counters[0];
			};
			*/

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

		private void ConfigWindow_Loaded(object sender, RoutedEventArgs e)
		{
			// Ensure layout is fully measured
			this.UpdateLayout();

			// Select first item if needed
			var vm = (ConfigViewModel)DataContext;
			if (vm.Counters.Count > 0 && vm.Selected == null)
				vm.Selected = vm.Counters[0];

			// Re-measure after selection (important!)
			this.UpdateLayout();

			var mouse = System.Windows.Forms.Control.MousePosition;

			const int offsetX = 20;
			const int offsetY = 20;

			double targetLeft = mouse.X + offsetX;
			double targetTop = mouse.Y + offsetY;

			var screen = System.Windows.Forms.Screen.FromPoint(mouse).WorkingArea;

			if (targetLeft + ActualWidth > screen.Right)
				targetLeft = screen.Right - ActualWidth;

			if (targetTop + ActualHeight > screen.Bottom)
				targetTop = screen.Bottom - ActualHeight;

			if (targetLeft < screen.Left)
				targetLeft = screen.Left;

			if (targetTop < screen.Top)
				targetTop = screen.Top;

			Left = targetLeft;
			Top = targetTop;

			// Lock size after initial placement
			this.SizeToContent = SizeToContent.Manual;
		}
	}
}
