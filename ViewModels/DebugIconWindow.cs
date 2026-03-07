using PerformanceTrayMonitor.Configuration;
using PerformanceTrayMonitor.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PerformanceTrayMonitor.Debugging
{
	public sealed class DebugIconWindow : Window
	{
		private ComboBox _setSelector;
		private Border _diagCard;
		private TextBlock _diagHeader;
		private TextBlock _diagMeta;
		private Expander _diagErrors;
		private WrapPanel _spriteGrid;

		public static bool IsOpen { get; private set; }

		public DebugIconWindow()
		{
			Title = "Icon Preview";
			SizeToContent = SizeToContent.WidthAndHeight;
			MinWidth = 400;
			MinHeight = 300;
			IsOpen = true;

			var root = new Grid { Margin = new Thickness(10) };

			// Apply shared gradient background
			root.Background = (Brush)Application.Current.Resources["AppWindowGradient"];

			root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // selector
			root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // diagnostics card
			root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // sprite grid

			// ---------------------------------------------------------
			// Icon set selector
			// ---------------------------------------------------------
			_setSelector = new ComboBox
			{
				Width = 180,
				FontSize = 13,
				Padding = new Thickness(4, 2, 4, 2),
				Margin = new Thickness(0, 0, 0, 6),
				ItemsSource = IconSetConfig.IconSets.Keys.OrderBy(k => k)
			};

			Loaded += (_, __) =>
			{
				if (_setSelector.Items.Count > 0)
					_setSelector.SelectedIndex = 0;
			};

			_setSelector.SelectionChanged += SetSelector_SelectionChanged;
			Grid.SetRow(_setSelector, 0);
			root.Children.Add(_setSelector);

			// ---------------------------------------------------------
			// Diagnostics card
			// ---------------------------------------------------------
			_diagCard = new Border
			{
				Padding = new Thickness(12),
				Margin = new Thickness(0, 0, 0, 8),
				CornerRadius = new CornerRadius(6),
				BorderThickness = new Thickness(2)
			};

			var diagStack = new StackPanel();
			_diagCard.Child = diagStack;

			_diagHeader = new TextBlock
			{
				FontWeight = FontWeights.Bold,
				FontSize = 16,
				Margin = new Thickness(0, 0, 0, 4)
			};

			_diagMeta = new TextBlock
			{
				TextWrapping = TextWrapping.Wrap,
				Margin = new Thickness(0, 0, 0, 4)
			};

			_diagErrors = new Expander
			{
				Header = "Errors",
				IsExpanded = false,
				Visibility = Visibility.Collapsed
			};

			diagStack.Children.Add(_diagHeader);
			diagStack.Children.Add(_diagMeta);
			diagStack.Children.Add(_diagErrors);

			Grid.SetRow(_diagCard, 1);
			root.Children.Add(_diagCard);

			// ---------------------------------------------------------
			// Sprite grid (wrap panel)
			// ---------------------------------------------------------
			_spriteGrid = new WrapPanel
			{
				Orientation = Orientation.Horizontal,
				ItemWidth = 32,
				ItemHeight = 32,
				Margin = new Thickness(0, 6, 0, 0)
			};

			var scroll = new ScrollViewer
			{
				Content = _spriteGrid,
				VerticalScrollBarVisibility = ScrollBarVisibility.Auto
			};
			scroll.Padding = new Thickness(2);

			Grid.SetRow(scroll, 2);
			root.Children.Add(scroll);

			Content = root;
		}

		protected override void OnClosed(EventArgs e)
		{
			base.OnClosed(e);
			IsOpen = false;
		}

		public static void CloseAll()
		{
			foreach (var win in Application.Current.Windows.OfType<DebugIconWindow>())
				win.Close();
		}

		// ---------------------------------------------------------
		// Selection changed → update diagnostics + sprite grid
		// ---------------------------------------------------------
		private void SetSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			Log.Debug($"SetSelector_SelectionChanged _setSelector.SelectedItem = {_setSelector.SelectedItem}");
			if (_setSelector.SelectedItem is not string setName)
				return;

			var set = IconSetConfig.IconSets[setName];
			var diag = IconSetDiagnosticsBuilder.Build(set);

			// -----------------------------------------------------
			// Diagnostics card styling
			// -----------------------------------------------------
			_diagCard.BorderBrush = diag.IsValid
				? new SolidColorBrush(Color.FromRgb(0x3A, 0xA6, 0x3A)) // green
				: new SolidColorBrush(Color.FromRgb(0xD4, 0x3C, 0x3C)); // red

			_diagHeader.Text = diag.IsValid
				? $"{diag.Name} — VALID ✔"
				: $"{diag.Name} — INVALID ✖";

			_diagMeta.Text =
				$"Embedded: {diag.IsEmbedded}\n" +
				$"Prefix: {diag.Prefix}\n" +
				$"Frames: {diag.FrameCount}\n" +
				$"Dimensions: {diag.Dimensions}";

			// -----------------------------------------------------
			// Error list
			// -----------------------------------------------------
			if (diag.Errors.Count > 0)
			{
				var errorStack = new StackPanel();
				foreach (var err in diag.Errors)
					errorStack.Children.Add(new TextBlock { Text = "• " + err });

				_diagErrors.Content = errorStack;
				_diagErrors.Visibility = Visibility.Visible;
			}
			else
			{
				_diagErrors.Visibility = Visibility.Collapsed;
			}

			// -----------------------------------------------------
			// Sprite grid preview
			// -----------------------------------------------------
			_spriteGrid.Children.Clear();

			foreach (var uri in set.Frames)
			{
				Log.Debug($"SetSelector_SelectionChanged uri = {uri}");
				BitmapImage bmp = null;

				try
				{
					using var stream = IconLoader.TryOpenStream(set, uri);

					if (stream != null)
					{
						bmp = new BitmapImage();
						bmp.BeginInit();
						bmp.StreamSource = stream;
						bmp.CacheOption = BitmapCacheOption.OnLoad;
						bmp.EndInit();
					}
					else
					{
						Log.Debug($"SetSelector_SelectionChanged: failed to load {uri}");
					}
				}
				catch (Exception ex)
				{
					Log.Debug(ex, $"Skipping IconSet {uri}");
					bmp = null;
				}

				var border = new Border
				{
					Width = 48,
					Height = 48,
					Margin = new Thickness(4),
					BorderBrush = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
					BorderThickness = new Thickness(0.5),
					ToolTip = uri,
					CornerRadius = new CornerRadius(3),
					Background = Brushes.Transparent
				};

				border.MouseEnter += (_, __) =>
				{
					border.Background = new SolidColorBrush(Color.FromRgb(0xEE, 0xF2, 0xF7)); // light blue
				};

				border.MouseLeave += (_, __) =>
				{
					border.Background = Brushes.Transparent;
				};

				border.Child = new Image
				{
					Width = 32,
					Height = 32,
					HorizontalAlignment = HorizontalAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center,
					Source = bmp
				};

				_spriteGrid.Children.Add(border);
			}
		}
	}
}
