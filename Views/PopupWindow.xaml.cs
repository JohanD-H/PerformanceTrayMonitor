using PerformanceTrayMonitor.Common;
using PerformanceTrayMonitor.Extensions;
using PerformanceTrayMonitor.ViewModels;
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

namespace PerformanceTrayMonitor.Views
{
	public partial class PopupWindow : Window
	{
		private bool _isClosingAnimated;
		private readonly Dictionary<object, (TextBlock NameBlock, Ellipse Dot)> _visualCache = new();
		public ICommand TogglePopupPinnedCommand { get; }
		public ICommand ClosePopupCommand { get; }

		public PopupWindow(MainViewModel vm)
		{
			DataContext = vm;

			// Create the command BEFORE InitializeComponent so XAML can bind to it
			TogglePopupPinnedCommand = new RelayCommand(_ =>
			{
				if (DataContext is MainViewModel vm)
				{
					vm.PopupPinned = !vm.PopupPinned;
				}
			});

			ClosePopupCommand = new RelayCommand(_ =>
			{
				Close();
			});

			InitializeComponent();
			Opacity = 0;

			Log.Debug($"Popup DataContext instance: {DataContext?.GetHashCode()}");

			Loaded += (_, __) =>
			{
				MetricsList.DataContext = DataContext;

				Width = MinWidth;

				Focus();
				Keyboard.Focus(this);

				MetricsList.ItemContainerGenerator.StatusChanged += (_, __) =>
				{
					if (MetricsList.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
					{
						// ⭐ Force each CounterVM to re-notify its History property
						foreach (var vm in ((MainViewModel)DataContext).Counters)
						{
							Log.Debug($"Loaded: VM hash = {vm?.GetHashCode()}");
							vm.ForceNotifyHistory();
						}

						Dispatcher.InvokeAsync(
							() =>
							{
								BuildVisualCache();
								ApplyAccentColors();
							},
							DispatcherPriority.ApplicationIdle
						);
					}
				};
			};
		}

		private void ForceSparklineRedraw()
		{
			foreach (var item in MetricsList.Items)
			{
				var container = (FrameworkElement)MetricsList
					.ItemContainerGenerator
					.ContainerFromItem(item);

				if (container == null)
					continue;

				var spark = FindChild<Views.SparkLine>(container);
				spark?.InvalidateVisual();
			}
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

		private void MetricName_Click(object sender, MouseButtonEventArgs e)
		{
			if (sender is TextBlock tb &&
				tb.DataContext is CounterViewModel vm &&
				DataContext is MainViewModel main)
			{
				main.ShowGraph(vm);
			}
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
				var (brush, shadowOpacity) = UIColors.GetSoftColorFor(name);

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

		private void DisplayNameBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (DataContext is MainViewModel main &&
				(sender as FrameworkElement)?.DataContext is CounterViewModel vm)
			{
				main.ShowGraph(vm);
			}
		}

		private void ShowGraph_Click(object sender, MouseButtonEventArgs e)
		{
			if (DataContext is MainViewModel main && 
				sender is FrameworkElement fe && fe.DataContext is CounterViewModel vm)
				main.ShowGraph(vm);
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
	}
}