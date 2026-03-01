using System.Windows;
using Serilog;

// -------------------
// About window
// -------------------
namespace PerformanceTrayMonitor.Views
{
	public partial class AboutWindow : Window
	{
		public AboutWindow()
		{
			InitializeComponent();
			Log.Debug("Initialize about..");
		}

		private void Close_Click(object sender, RoutedEventArgs e)
		{
			Log.Debug("Closing about..");
			Close();
		}
	}
}
