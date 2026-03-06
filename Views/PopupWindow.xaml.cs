using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace PerformanceTrayMonitor.Views
{
	public partial class PopupWindow : Window
	{
		private bool _isClosingAnimated;

		public PopupWindow()
		{
			InitializeComponent();
			Opacity = 0; // invisible until animation

			Loaded += (_, __) =>
			{
				Width = MinWidth; // lock horizontal resizing
			};
		}

		// ------------------------------------------------------------
		// Now animate AFTER content is rendered
		// ------------------------------------------------------------
		protected override void OnContentRendered(EventArgs e)
		{
			base.OnContentRendered(e);

			// Fade-in
			var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
			{
				EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
			};
			BeginAnimation(Window.OpacityProperty, fadeIn);
		}

		// ------------------------------------------------------------
		// FADE-OUT
		// ------------------------------------------------------------
		protected override void OnClosing(CancelEventArgs e)
		{
			if (_isClosingAnimated)
			{
				base.OnClosing(e);
				return;
			}

			e.Cancel = true;
			_isClosingAnimated = true;

			var fadeOut = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(150))
			{
				EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
			};

			fadeOut.Completed += (_, _) =>
			{
				BeginAnimation(Window.OpacityProperty, null);
				Close();
			};

			BeginAnimation(Window.OpacityProperty, fadeOut);
		}

		private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (e.ButtonState == MouseButtonState.Pressed)
				DragMove();
		}

		private void CloseButton_Click(object sender, RoutedEventArgs e)
		{
			Close(); // triggers your fade-out OnClosing override
		}

	}
}
