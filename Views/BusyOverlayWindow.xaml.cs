using System;
using System.Windows;
using PerformanceTrayMonitor.Common;

namespace PerformanceTrayMonitor.Views
{
	public partial class BusyOverlayWindow : Window
	{
		public BusyOverlayWindow(Window owner)
		{
			InitializeComponent();

			Owner = owner;
			Log.Debug($"BusyOverlayWindow: Owner = '{Owner}'");
			WindowStartupLocation = WindowStartupLocation.CenterOwner;
			Topmost = true;
		}
		/*
		public BusyOverlayWindow(Window owner)
		{
			InitializeComponent();

			//Owner = owner;
			// This will center on primary screen.
			WindowStartupLocation = WindowStartupLocation.CenterScreen;
			// Topmost = true;

			// Alternate option:
			//Owner = configWindow;
			//WindowStartupLocation = WindowStartupLocation.CenterOwner;

			//if (Owner != null)
			//{
			//	Owner.IsEnabled = false;
			//	Owner.LocationChanged += Owner_LocationChanged;
			//}

			// Loaded += (_, __) => CenterOnOwner();
			//Loaded += (_, __) =>
			//{
			//	Dispatcher.BeginInvoke(new Action(CenterOnOwner), System.Windows.Threading.DispatcherPriority.ContextIdle);
			//};
		}
		*/

		private void CenterOnOwner()
		{
			if (Owner == null)
				return;

			Left = Owner.Left + (Owner.Width - ActualWidth) / 2;
			Top = Owner.Top + (Owner.Height - ActualHeight) / 2;
			Log.Debug($"CenterOnOwner: Left = {Left}, Top={Top}");
		}

		private void Owner_LocationChanged(object? sender, EventArgs e)
		{
			CenterOnOwner();
		}

		protected override void OnClosed(EventArgs e)
		{
			base.OnClosed(e);

			if (Owner != null)
			{
				Owner.IsEnabled = true;
				Owner.LocationChanged -= Owner_LocationChanged;
			}
		}
	}
}
