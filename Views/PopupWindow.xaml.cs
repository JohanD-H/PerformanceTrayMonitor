using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using MediaColor = System.Windows.Media.Color;

namespace PerformanceTrayMonitor.Views
{
	public partial class PopupWindow : Window
	{
		private bool _isClosingAnimated;
		private readonly Dictionary<object, (TextBlock NameBlock, Ellipse Dot)> _visualCache = new();

		public PopupWindow()
		{
			InitializeComponent();
			Opacity = 0;

			Loaded += (_, __) =>
			{
				Width = MinWidth;

				MetricsList.ItemContainerGenerator.StatusChanged += (_, __) =>
				{
					if (MetricsList.ItemContainerGenerator.Status ==
						GeneratorStatus.ContainersGenerated)
					{
						Dispatcher.BeginInvoke(new Action(() =>
						{
							BuildVisualCache();
							ApplyAccentColors();
						}), DispatcherPriority.Loaded);
					}
				};
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

			var duration = TimeSpan.FromMilliseconds(150);
			var ease = new QuadraticEase { EasingMode = EasingMode.EaseIn };

			// Fade-out
			var fadeOut = new DoubleAnimation(Opacity, 0, duration)
			{
				EasingFunction = ease
			};

			// Scale-out X
			var scaleX = new DoubleAnimation(1.0, 0.98, duration)
			{
				EasingFunction = ease
			};

			// Scale-out Y
			var scaleY = new DoubleAnimation(1.0, 0.98, duration)
			{
				EasingFunction = ease
			};

			fadeOut.Completed += (_, _) =>
			{
				// Clear transform animations so next open starts cleanly
				Root.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
				Root.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);

				// Clear opacity animation and close
				BeginAnimation(Window.OpacityProperty, null);
				Close();
			};

			BeginAnimation(Window.OpacityProperty, fadeOut);
			Root.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
			Root.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
		}

		private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (e.ButtonState == MouseButtonState.Pressed)
				DragMove();
		}

		private void CloseButton_Click(object sender, RoutedEventArgs e)
		{
			Close(); // triggers fade-out OnClosing override
		}

		private void ApplyAccentColors()
		{
			//Dispatcher.BeginInvoke(new Action(() =>
			//{
				ApplyAccentColorsCore();
			//}), DispatcherPriority.Loaded);
		}

		private void ApplyAccentColorsCore()
		{
			foreach (var kvp in _visualCache)
			{
				var (displayNameBlock, dot) = kvp.Value;

				var name = displayNameBlock.Text;
				var (brush, shadowOpacity) = GetSoftColorFor(name);

				displayNameBlock.Foreground = brush;

				displayNameBlock.Effect = new DropShadowEffect
				{
					Color = Colors.Black,
					BlurRadius = 1.5,
					ShadowDepth = 0,
					Opacity = shadowOpacity
				};

				// dot.Fill = brush; // optional
			}
		}

		private void BuildVisualCache()
		{
			_visualCache.Clear();

			foreach (var item in MetricsList.Items)
			{
				var container = (FrameworkElement)MetricsList
					.ItemContainerGenerator
					.ContainerFromItem(item);

				if (container == null)
					continue;

				var nameBlock = FindChild<TextBlock>(container, tb => tb.Name == "DisplayNameBlock");
				var dot = FindChild<Ellipse>(container);

				if (nameBlock != null)
					_visualCache[item] = (nameBlock, dot);
			}
		}

		private T? FindChild<T>(DependencyObject parent, Func<T, bool>? predicate = null) where T : DependencyObject
		{
			int count = VisualTreeHelper.GetChildrenCount(parent);
			for (int i = 0; i < count; i++)
			{
				var child = VisualTreeHelper.GetChild(parent, i);

				if (child is T typed)
				{
					if (predicate == null || predicate(typed))
						return typed;
				}

				var result = FindChild(child, predicate);
				if (result != null)
					return result;
			}
			return null;
		}

		// With luminance compensation, colors in a UI are hard....
		private (SolidColorBrush Brush, double ShadowOpacity) GetSoftColorFor(string name)
		{
			int hash = name.GetHashCode();
			double hue = (hash & 0xFFFFFF) % 360;
			double saturation = 0.80;


			double hueNorm = hue / 360.0;
			double compensation =
				0.10 * Math.Sin((hueNorm * 2 * Math.PI) - Math.PI / 2);

			// Boost saturation slightly for green/mint hues (120–160°)
			if (hue >= 120 && hue <= 160)
			{
				saturation = Math.Min(1.0, saturation + 0.15);
			}
			// Boost saturation slightly for blue/violet hues (240–280°)
			if (hue >= 240 && hue <= 280)
			{
				saturation = Math.Min(1.0, saturation + 0.15);
			}

			double lightness = 0.20 + compensation;
			lightness = Math.Clamp(lightness, 0.40, 0.60);

			double shadowOpacity = 0.25 + (0.20 * (0.60 - lightness));

			var c = HslToColor(hue, saturation, lightness);
			return (new SolidColorBrush(c), shadowOpacity);
		}

		private MediaColor HslToColor(double h, double s, double l)
		{
			h /= 360.0;

			double r = l, g = l, b = l;
			if (s != 0)
			{
				double temp2 = (l < 0.5) ? l * (1.0 + s) : (l + s) - (l * s);
				double temp1 = 2.0 * l - temp2;

				r = HueToRgb(temp1, temp2, h + 1.0 / 3.0);
				g = HueToRgb(temp1, temp2, h);
				b = HueToRgb(temp1, temp2, h - 1.0 / 3.0);
			}

			return MediaColor.FromRgb((byte)(255 * r), (byte)(255 * g), (byte)(255 * b));
		}

		private double HueToRgb(double t1, double t2, double hue)
		{
			if (hue < 0) hue += 1;
			if (hue > 1) hue -= 1;

			if (6 * hue < 1) return t1 + (t2 - t1) * 6 * hue;
			if (2 * hue < 1) return t2;
			if (3 * hue < 2) return t1 + (t2 - t1) * ((2.0 / 3.0) - hue) * 6;

			return t1;
		}
	}
}