using PerformanceTrayMonitor.Configuration;
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
			Title = "Icon Debugger";
			Width = 900;
			Height = 700;
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
				Width = 250,
				Margin = new Thickness(0, 0, 0, 10),
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
				Margin = new Thickness(0, 0, 0, 10),
				CornerRadius = new CornerRadius(6),
				BorderThickness = new Thickness(2)
			};

			var diagStack = new StackPanel();
			_diagCard.Child = diagStack;

			_diagHeader = new TextBlock
			{
				FontWeight = FontWeights.Bold,
				FontSize = 16,
				Margin = new Thickness(0, 0, 0, 6)
			};

			_diagMeta = new TextBlock
			{
				TextWrapping = TextWrapping.Wrap,
				Margin = new Thickness(0, 0, 0, 6)
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
				ItemWidth = 48,
				ItemHeight = 48,
				Margin = new Thickness(0, 10, 0, 0)
			};

			var scroll = new ScrollViewer
			{
				Content = _spriteGrid,
				VerticalScrollBarVisibility = ScrollBarVisibility.Auto
			};

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
				BitmapImage bmp = null;

				try
				{
					if (uri.StartsWith("/"))
					{
						var info = Application.GetResourceStream(new Uri(uri, UriKind.Relative));
						if (info != null)
						{
							bmp = new BitmapImage();
							bmp.BeginInit();
							bmp.StreamSource = info.Stream;
							bmp.CacheOption = BitmapCacheOption.OnLoad;
							bmp.EndInit();
						}
					}
					else
					{
						bmp = new BitmapImage(new Uri(uri));
					}
				}
				catch
				{
					bmp = null;
				}

				var border = new Border
				{
					Width = 48,
					Height = 48,
					Margin = new Thickness(4),
					BorderBrush = Brushes.Gray,
					BorderThickness = new Thickness(1),
					ToolTip = uri,
					Background = Brushes.Transparent
				};

				border.MouseEnter += (_, __) =>
				{
					border.Background = new SolidColorBrush(Color.FromRgb(0xD0, 0xE8, 0xFF)); // light blue
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
