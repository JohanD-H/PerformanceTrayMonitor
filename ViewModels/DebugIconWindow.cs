using PerformanceTrayMonitor.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

// ---------------------------------------------------------------
// Show the Icon frames, mainly for debugging, for now leave it in
// ---------------------------------------------------------------
namespace PerformanceTrayMonitor.Debugging
{
	public sealed class DebugIconWindow : Window
	{
		private ComboBox _setSelector;
		private ItemsControl _frameList;
		public static bool IsOpen { get; private set; }

		public DebugIconWindow()
		{
			Title = "Icon Debugger";
			Width = 800;
			Height = 600;
			IsOpen = true;

			// Root layout
			var root = new Grid
			{
				Margin = new Thickness(10)
			};

			root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
			root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

			// Dropdown
			_setSelector = new ComboBox
			{
				Width = 200,
				Margin = new Thickness(0, 0, 0, 10),
				ItemsSource = IconSetConfig.IconSets.Keys.OrderBy(k => k)
			};
			_setSelector.SelectionChanged += SetSelector_SelectionChanged;

			// Frame list
			_frameList = new ItemsControl();

			var scroll = new ScrollViewer
			{
				Content = _frameList
			};

			// Add to grid
			root.Children.Add(_setSelector);
			Grid.SetRow(scroll, 1);
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

		private void SetSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (_setSelector.SelectedItem is not string setName)
				return;

			var set = IconSetConfig.IconSets[setName];
			var frames = new List<FrameInfo>();

			foreach (var uri in set.Frames)
			{
				try
				{
					BitmapImage bmp;

					if (uri.StartsWith("/"))
					{
						// Embedded resource
						var info = System.Windows.Application.GetResourceStream(new Uri(uri, UriKind.Relative));
						if (info == null)
						{
							frames.Add(new FrameInfo(uri, null));
							continue;
						}

						bmp = new BitmapImage();
						bmp.BeginInit();
						bmp.StreamSource = info.Stream;
						bmp.CacheOption = BitmapCacheOption.OnLoad;
						bmp.EndInit();
					}
					else
					{
						// External file
						bmp = new BitmapImage(new Uri(uri));
					}

					frames.Add(new FrameInfo(uri, bmp));
				}
				catch
				{
					frames.Add(new FrameInfo(uri, null));
				}
			}

			// Build UI elements dynamically
			_frameList.ItemsSource = frames.Select(f =>
			{
				var panel = new StackPanel
				{
					Orientation = Orientation.Horizontal,
					Margin = new Thickness(0, 5, 0, 5)
				};

				var img = new Image
				{
					Width = 32,
					Height = 32,
					Margin = new Thickness(0, 0, 10, 0),
					Source = f.Preview
				};

				var text = new TextBlock
				{
					Text = f.Uri,
					VerticalAlignment = VerticalAlignment.Center
				};

				panel.Children.Add(img);
				panel.Children.Add(text);

				return panel;
			}).ToList();
		}

		private sealed class FrameInfo
		{
			public string Uri { get; }
			public BitmapImage Preview { get; }

			public FrameInfo(string uri, BitmapImage preview)
			{
				Uri = uri;
				Preview = preview;
			}
		}
	}
}
