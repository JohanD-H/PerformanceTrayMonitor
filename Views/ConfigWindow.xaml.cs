using PerformanceTrayMonitor.ViewModels;
using PerformanceTrayMonitor.Common;
using System.ComponentModel;
using System.Diagnostics;
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
			
			((ConfigViewModel)DataContext).OwnerWindow = this;

			this.Closing += ConfigWindow_Closing;

			Loaded += (_, __) => vm.StartPreviewTimer();

			vm.ConfirmCancel = () =>
				MessageBox.Show(
					"Discard all changes?",
					"Confirm",
					MessageBoxButton.YesNo,
					MessageBoxImage.Warning
				) == MessageBoxResult.Yes;

			vm.ConfirmClose = () =>
				MessageBox.Show(
					"You have unsaved changes. Close anyway?",
					"Confirm",
					MessageBoxButton.YesNo,
					MessageBoxImage.Warning
				) == MessageBoxResult.Yes;

			vm.ConfirmReset = () =>
				MessageBox.Show(
					"Reset all metrics to default?",
					"Confirm",
					MessageBoxButton.YesNo,
					MessageBoxImage.Warning
				) == MessageBoxResult.Yes;

			vm.RequestClose = () => this.Close();
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			if (DataContext is ConfigViewModel vm)
			{
				Debug.WriteLine($"UI DataContext hash = {vm.GetHashCode()}");
				Debug.WriteLine($"UI Editor hash = {vm.Editor?.GetHashCode() ?? 0}");
			}
		}

		private void ButtonFlashHandler(object sender, RoutedEventArgs e)
		{
			if (FindResource("ButtonFlashStoryboard") is Storyboard sb)
				sb.Begin((FrameworkElement)sender);

			// Ensure the Click event continues to bubble
			e.Handled = false;
		}

		private void ApplyButton_Click(object sender, RoutedEventArgs e)
		{
			if (FindResource("PanelPulseStoryboard") is System.Windows.Media.Animation.Storyboard sb)
			{
				sb.Begin(DetailsPanel);
			}
		}

		private void ConfigWindow_Closing(object sender, CancelEventArgs e)
		{
			if (DataContext is not ConfigViewModel vm)
				return;

			// ------------------------------------------------------------
			// 1. Ask the ViewModel whether closing is allowed
			// ------------------------------------------------------------
			if (vm.EditorPendingEdits || vm.GlobalEditsPending)
			{
				bool allow = vm.ConfirmClose?.Invoke() ?? true;

				if (!allow)
				{
					e.Cancel = true;
					return;
				}
			}

			// ------------------------------------------------------------
			// 2. Closing is allowed → clean up
			// ------------------------------------------------------------
			vm.StopPreviewTimer();
			vm.CancelAllWork();
		}
		/*
		private void ConfigWindow_Closing(object sender, CancelEventArgs e)
		{
			var vm = DataContext as ConfigViewModel;
			if (vm == null)
				return;

			// Ask the ViewModel whether closing is allowed
			if (vm.HasPendingEdits || vm.HasAppliedChanges)
			{
				if (!(vm.ConfirmClose?.Invoke() ?? true))
				{
					e.Cancel = true;   // <-- STOP the window from closing
					return;
				}
			}

			// If we get here, closing is allowed
			vm.StopPreviewTimer();
			vm.CancelAllWork();
		}
		*/
	}
}
