using PerformanceTrayMonitor.Common;
using PerformanceTrayMonitor.ViewModels;
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

			Loaded += (_, __) =>
			{
				// Force the ComboBoxes to apply the DTO values AFTER they have items
				var vm = (ConfigViewModel)DataContext;
				vm.RefreshSelectionsAfterLoad();
			};

			vm.RequestClose += () => this.Close();

			vm.ConfirmReset = () =>
			{
				var result = MessageBox.Show(
					"This will remove all your current tracked metrics and restore the default set.\n\n" +
					"This action cannot be undone.\n\n" +
					"Do you want to continue?",
					"Reset to Defaults",
					MessageBoxButton.YesNo,
					MessageBoxImage.Warning);

				return result == MessageBoxResult.Yes;
			};
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
