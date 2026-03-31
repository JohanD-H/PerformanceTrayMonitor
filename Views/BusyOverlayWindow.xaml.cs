using System;
using System.Windows;

namespace PerformanceTrayMonitor.Views
{
	public partial class BusyOverlayWindow : Window
	{
		public BusyOverlayWindow(Window owner)
		{
			InitializeComponent();

			Owner = owner;
			WindowStartupLocation = WindowStartupLocation.CenterOwner;
			Topmost = true;
		}

		private void CenterOnOwner()
		{
			if (Owner == null)
				return;

			Left = Owner.Left + (Owner.Width - ActualWidth) / 2;
			Top = Owner.Top + (Owner.Height - ActualHeight) / 2;
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
