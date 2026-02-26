using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Animation;

namespace PerformanceTrayMonitor.Views
{
	public partial class PopupWindow : Window
	{
		private bool _isClosingAnimated;

		public PopupWindow()
		{
			InitializeComponent();
			Opacity = 0; // start transparent for fade‑in
		}

		protected override void OnContentRendered(EventArgs e)
		{
			base.OnContentRendered(e);

			// 1. Let WPF measure the content first
			this.UpdateLayout();

			// 2. Clamp to screen working area
			var screen = SystemParameters.WorkArea;

			if (Width > screen.Width)
				Width = screen.Width;

			if (Height > screen.Height)
				Height = screen.Height;

			// Optional: add a margin so it never touches screen edges
			const double margin = 20;
			Width = Math.Min(Width, screen.Width - margin);
			Height = Math.Min(Height, screen.Height - margin);

			// 3. Fade‑in animation
			var fadeIn = new DoubleAnimation
			{
				From = 0,
				To = 1,
				Duration = TimeSpan.FromMilliseconds(180),
				EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
			};

			BeginAnimation(Window.OpacityProperty, fadeIn);
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			// If animation already ran, allow normal close
			if (_isClosingAnimated)
			{
				base.OnClosing(e);
				return;
			}

			// First close attempt → run fade‑out
			e.Cancel = true;
			_isClosingAnimated = true;

			var fadeOut = new DoubleAnimation
			{
				From = Opacity,
				To = 0,
				Duration = TimeSpan.FromMilliseconds(150),
				EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
			};

			fadeOut.Completed += (_, _) =>
			{
				// Remove animation to avoid re‑triggering
				BeginAnimation(Window.OpacityProperty, null);

				// Now close for real — this time _isClosingAnimated = true
				Close();
			};

			BeginAnimation(Window.OpacityProperty, fadeOut);
		}
	}
}
