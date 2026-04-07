using PerformanceTrayMonitor.Common;
using PerformanceTrayMonitor.Configuration;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PerformanceTrayMonitor.Views
{
	public partial class DebugIconWindow : Window
	{
		public ICommand CloseCommand { get; }

		public static bool IsOpen { get; private set; }

		public DebugIconWindow(string iconSetName)
		{
			CloseCommand = new RelayCommand(_ => Close());

			InitializeComponent();
			IsOpen = true;

			ZoomSlider.ValueChanged += (_, __) => ApplyZoom();

			// Populate selector
			SetSelector.ItemsSource = IconSetConfig.IconSets.Keys.OrderBy(k => k);

			Loaded += (_, __) =>
			{
				if (IconSetConfig.IconSets.ContainsKey(iconSetName))
					SetSelector.SelectedItem = iconSetName;

				Activate();
				Focus();
				Keyboard.Focus(this);
			};
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

		// ==========================================================
		// Selection changed → update diagnostics + sprite grid
		// ==========================================================
		private void SetSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (SetSelector.SelectedItem is not string setName)
				return;

			var set = IconSetConfig.IconSets[setName];
			var diag = IconSetDiagnosticsBuilder.Build(set);

			// Diagnostics card
			DiagCard.BorderBrush = diag.IsValid
				? new SolidColorBrush(Color.FromRgb(0x3A, 0xA6, 0x3A))
				: new SolidColorBrush(Color.FromRgb(0xD4, 0x3C, 0x3C));

			DiagHeader.Text = diag.IsValid
				? $"{diag.Name} — VALID ✔"
				: $"{diag.Name} — INVALID ✖";

			DiagMeta.Text =
				$"Embedded: {diag.IsEmbedded}\n" +
				$"Prefix: {diag.Prefix}\n" +
				$"Frames: {diag.FrameCount}\n" +
				$"Dimensions: {diag.Dimensions}";

			// Errors
			if (diag.Errors.Count > 0)
			{
				var stack = new StackPanel();
				foreach (var err in diag.Errors)
					stack.Children.Add(new TextBlock { Text = "• " + err });

				DiagErrors.Content = stack;
				DiagErrors.Visibility = Visibility.Visible;
			}
			else
			{
				DiagErrors.Visibility = Visibility.Collapsed;
			}

			// Sprite grid
			SpriteGrid.Children.Clear();

			foreach (var uri in set.Frames)
			{
				BitmapImage? bmp = null;

				try
				{
					using var stream = IconLoader.TryOpenStream(set, uri);

					if (stream != null)
					{
						bmp = new BitmapImage();
						bmp.BeginInit();
						bmp.StreamSource = stream;
						bmp.CacheOption = BitmapCacheOption.OnLoad;
						bmp.DecodePixelWidth = 64;
						bmp.DecodePixelHeight = 64;
						bmp.EndInit();
					}
				}
				catch
				{
					// Nothing
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

				border.LayoutTransform = new ScaleTransform(1.0, 1.0);

				border.MouseEnter += (_, __) =>
					border.Background = new SolidColorBrush(Color.FromRgb(0xEE, 0xF2, 0xF7));

				border.MouseLeave += (_, __) =>
					border.Background = Brushes.Transparent;

				border.Child = new Image
				{
					Stretch = Stretch.Uniform,
					HorizontalAlignment = HorizontalAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center,
					Source = bmp
				};

				SpriteGrid.Children.Add(border);
			}

			ApplyZoom();

			// Stats
			var stats = IconSetStatistics.Compute();

			StatusFrames.Text = $"Emb: {stats.Embedded}";
			StatusValid.Text = $"Ext: {stats.External}";
			StatusValidState.Text = $"Val: {stats.Valid}";
			StatusInvalidState.Text = $"Inv: {stats.Invalid}";

			StatusSetFrames.Text = $"Frames: {diag.FrameCount}";
			StatusSetValidity.Text = diag.IsValid ? "Valid" : "Invalid";
			StatusSetState.Text = diag.IsValid ? "Ready" : "Errors detected";
		}

		private void ApplyZoom()
		{
			double scale = ZoomSlider.Value / 64.0;

			foreach (Border border in SpriteGrid.Children.OfType<Border>())
			{
				if (border.LayoutTransform is ScaleTransform st)
				{
					st.ScaleX = scale;
					st.ScaleY = scale;
				}
			}
		}
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
