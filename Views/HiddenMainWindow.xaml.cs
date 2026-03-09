using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace PerformanceTrayMonitor.Views
{
	public partial class HiddenMainWindow : Window
	{
		public HiddenMainWindow()
		{
			InitializeComponent();
			Loaded += OnLoaded;
		}

		private void OnLoaded(object sender, RoutedEventArgs e)
		{
			var hwnd = new WindowInteropHelper(this).Handle;

			int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

			exStyle |= WS_EX_TOOLWINDOW;
			exStyle &= ~WS_EX_APPWINDOW;

			SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
		}

		private const int GWL_EXSTYLE = -20;
		private const int WS_EX_TOOLWINDOW = 0x00000080;
		private const int WS_EX_APPWINDOW = 0x00040000;

		[DllImport("user32.dll")]
		private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

		[DllImport("user32.dll")]
		private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
	}
}
