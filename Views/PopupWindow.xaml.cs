using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Animation;

namespace PerformanceTrayMonitor.Views
{
	public partial class PopupWindow : Window
	{
		private bool _isClosingAnimated;

		private double _finalLeft;
		private double _finalTop;
		private double _startLeft;
		private double _startTop;

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
		// CRITICAL: Set initial position BEFORE window becomes visible
		// ------------------------------------------------------------
		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);

			//ComputePositions(out _finalLeft, out _finalTop, out _startLeft, out _startTop);

			// Set initial position BEFORE the OS shows the window
			Left = _startLeft;
			Top = _startTop;

			// Adapt to different monitor specs. (sizes).
			var screen = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Control.MousePosition);
			double screenWidth = screen.WorkingArea.Width;

			double targetWidth = screenWidth * 0.09; // 9% of screen width
			targetWidth = Math.Max(150, Math.Min(260, targetWidth)); // clamp

			Width = targetWidth;
			MinWidth = targetWidth;
			MaxWidth = targetWidth;
		}

		// ------------------------------------------------------------
		// Now animate AFTER content is rendered
		// ------------------------------------------------------------
		protected override void OnContentRendered(EventArgs e)
		{
			base.OnContentRendered(e);

			UpdateLayout(); // ensure ActualWidth/Height are correct

			// Clamp to working area
			var wa = SystemParameters.WorkArea;
			const double margin = 20;

			Width = Math.Min(ActualWidth, wa.Width - margin);
			Height = Math.Min(ActualHeight, wa.Height - margin);

			MaxHeight = Height;
			Height = Height; // force OS to recalc resize frame

			// NOW compute positions (ActualWidth is correct)
			ComputePositions(out _finalLeft, out _finalTop, out _startLeft, out _startTop);

			// Set initial position BEFORE animation
			Left = _startLeft;
			Top = _startTop;

			// Animate into final position
			AnimateIntoPosition(_finalLeft, _finalTop, _startLeft, _startTop);

			// Fade-in
			var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
			{
				EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
			};
			BeginAnimation(Window.OpacityProperty, fadeIn);
		}

		// ------------------------------------------------------------
		// POSITIONING LOGIC
		// ------------------------------------------------------------
		private void ComputePositions(out double finalLeft, out double finalTop, out double startLeft, out double startTop)
		{
			var mouse = System.Windows.Forms.Control.MousePosition;
			var screen = System.Windows.Forms.Screen.FromPoint(mouse);
			var wa = screen.WorkingArea;
			var bounds = screen.Bounds;

			const int offset = 12;

			bool taskbarBottom = bounds.Bottom > wa.Bottom;
			bool taskbarTop = bounds.Top < wa.Top;
			bool taskbarLeft = bounds.Left < wa.Left;
			bool taskbarRight = bounds.Right > wa.Right;

			if (taskbarBottom)
			{
				finalLeft = mouse.X - (Width / 2);
				finalTop = mouse.Y - Height - offset;

				startLeft = finalLeft;
				startTop = finalTop + 20;
			}
			else if (taskbarTop)
			{
				finalLeft = mouse.X - (Width / 2);
				finalTop = mouse.Y + offset;

				startLeft = finalLeft;
				startTop = finalTop - 20;
			}
			else if (taskbarLeft)
			{
				finalLeft = mouse.X + offset;
				finalTop = mouse.Y - (Height / 2);

				startLeft = finalLeft - 20;
				startTop = finalTop;
			}
			else if (taskbarRight)
			{
				finalLeft = mouse.X - Width - offset;
				finalTop = mouse.Y - (Height / 2);

				startLeft = finalLeft + 20;
				startTop = finalTop;
			}
			else
			{
				finalLeft = mouse.X - (Width / 2);
				finalTop = mouse.Y - Height - offset;

				startLeft = finalLeft;
				startTop = finalTop + 20;
			}

			// Clamp
			if (finalLeft + Width > wa.Right) finalLeft = wa.Right - Width;
			if (finalLeft < wa.Left) finalLeft = wa.Left;

			if (finalTop + Height > wa.Bottom) finalTop = wa.Bottom - Height;
			if (finalTop < wa.Top) finalTop = wa.Top;
		}

		private void AnimateIntoPosition(double finalLeft, double finalTop, double startLeft, double startTop)
		{
			var duration = TimeSpan.FromMilliseconds(180);
			var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };

			BeginAnimation(Window.LeftProperty,
				new DoubleAnimation(startLeft, finalLeft, duration) { EasingFunction = ease });

			BeginAnimation(Window.TopProperty,
				new DoubleAnimation(startTop, finalTop, duration) { EasingFunction = ease });
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
	}
}
