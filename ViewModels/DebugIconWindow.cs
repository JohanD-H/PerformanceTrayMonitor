using PerformanceTrayMonitor.Configuration;
using PerformanceTrayMonitor.Common;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PerformanceTrayMonitor.ViewModels
{
	public sealed class DebugIconWindow : Window
	{
		private ComboBox _setSelector;
		private Border _diagCard;
		private TextBlock _diagHeader;
		private TextBlock _diagMeta;
		private Expander _diagErrors;
		private WrapPanel _spriteGrid;

		private TextBlock _statusSetFrames;
		private TextBlock _statusSetValidity;
		private TextBlock _statusSetState;

		private TextBlock _statusFrames;
		private TextBlock _statusValid;
		private TextBlock _statusValidState;
		private TextBlock _statusInvalidState;

		public static bool IsOpen { get; private set; }

		public DebugIconWindow(string iconSetName)
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
			root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // status bar

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
				if (IconSetConfig.IconSets.ContainsKey(iconSetName))
					_setSelector.SelectedItem = iconSetName;
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

			// ---------------------------------------------------------
			// Status bar
			// ---------------------------------------------------------
			var statusBar = new Border
			{
				Height = 26,
				Margin = new Thickness(0, 8, 0, 0),
				Background = new SolidColorBrush(Color.FromArgb(0x80, 0xD0, 0xF0, 0xFF)),
				BorderBrush = new SolidColorBrush(Color.FromArgb(0x33, 0, 0, 0)),
				BorderThickness = new Thickness(1, 1, 1, 0)
			};

			var statusGrid = new Grid
			{
				Margin = new Thickness(8, 0, 8, 0)
			};

			statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // global
			statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // sepLeft
			statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // center
			statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // sepRight
			statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // state

			statusBar.Child = statusGrid;

			_statusFrames = new TextBlock
			{
				Foreground = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(0, 0, 8, 0)
			};

			_statusValid = new TextBlock
			{
				Foreground = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(0, 0, 8, 0)
			};

			_statusValidState = new TextBlock
			{
				Foreground = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(0, 0, 8, 0)
			};

			_statusInvalidState = new TextBlock
			{
				Foreground = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(0, 0, 8, 0)
			};

			_statusSetFrames = new TextBlock
			{
				Foreground = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(8, 0, 8, 0)
			};

			_statusSetValidity = new TextBlock
			{
				Foreground = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(0, 0, 8, 0)
			};

			_statusSetState = new TextBlock
			{
				Foreground = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(8, 0, 0, 0)
			};

			var globalPanel = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				VerticalAlignment = VerticalAlignment.Center,
				Opacity = 0.75
			};

			globalPanel.Children.Add(_statusFrames);
			globalPanel.Children.Add(_statusValid);
			globalPanel.Children.Add(_statusValidState);
			globalPanel.Children.Add(_statusInvalidState);

			Grid.SetColumn(globalPanel, 0);
			statusGrid.Children.Add(globalPanel);

			var sepLeft = new TextBlock
			{
				Text = "|",
				Margin = new Thickness(8, 0, 8, 0),
				VerticalAlignment = VerticalAlignment.Center,
				Opacity = 0.5
			};

			Grid.SetColumn(sepLeft, 1);
			statusGrid.Children.Add(sepLeft);

			var setPanel = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				VerticalAlignment = VerticalAlignment.Center,
				HorizontalAlignment = HorizontalAlignment.Center
			};

			setPanel.Children.Add(_statusSetFrames);
			setPanel.Children.Add(_statusSetValidity);

			Grid.SetColumn(setPanel, 2);
			statusGrid.Children.Add(setPanel);

			var sepRight = new TextBlock
			{
				Text = "|",
				Margin = new Thickness(8, 0, 8, 0),
				VerticalAlignment = VerticalAlignment.Center,
				Opacity = 0.5
			};

			Grid.SetColumn(sepRight, 3);
			statusGrid.Children.Add(sepRight);

			_statusSetState.VerticalAlignment = VerticalAlignment.Center;
			Grid.SetColumn(_statusSetState, 4);
			statusGrid.Children.Add(_statusSetState);

			Grid.SetRow(statusBar, 3);
			root.Children.Add(statusBar);

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
						bmp.DecodePixelWidth = 64;   // Match preview size
						bmp.DecodePixelHeight = 64;
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
					Width = 64,
					Height = 64,
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
					Stretch = Stretch.Uniform,
					HorizontalAlignment = HorizontalAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center,
					Source = bmp
				};

				_spriteGrid.Children.Add(border);
			}

			var stats = IconSetStatistics.Compute();

			_statusFrames.Text = $"Emb: {stats.Embedded}";
			_statusValid.Text  = $"Ext: {stats.External}";
			_statusValidState.Text = $"Val: {stats.Valid}";
			_statusInvalidState.Text = $"Inv: {stats.Invalid}";

			_statusSetFrames.Text = $"Frames: {diag.FrameCount}";
			_statusSetValidity.Text = diag.IsValid ? "Valid" : "Invalid";
			_statusSetState.Text = diag.IsValid ? "Ready" : "Errors detected";
		}

		public static class IconSetStatistics
		{
			public static (int Embedded, int External, int Valid, int Invalid) Compute()
			{
				var embedded = EmbeddedIconDiscovery.GetEmbeddedSets();
				var external = ExternalIconDiscovery.Discover();

				var all = embedded
					.Concat(external)
					.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

				int embeddedCount = embedded.Count;
				int externalCount = external.Count;

				int validCount = all.Values.Count(s => IconSetValidator.Validate(s));
				int invalidCount = all.Count - validCount;

				return (embeddedCount, externalCount, validCount, invalidCount);
			}
		}
	}
}
