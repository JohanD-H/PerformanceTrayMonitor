using PerformanceTrayMonitor.ViewModels;
using PerformanceTrayMonitor.Common;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

// --------------------------------------------
// Application tray icon animation
// --------------------------------------------
namespace PerformanceTrayMonitor.Configuration
{
	internal sealed class AnimatedTrayIcon : IDisposable
	{
		private readonly NotifyIcon _notifyIcon;
		private readonly DispatcherTimer _timer;
		private readonly List<Bitmap> _frames = new();
		private readonly List<Icon> _icons = new();
		private readonly ConfigViewModel _sharedConfigVm;
		private readonly MainViewModel _mainVm;

		private ContextMenu? _wpfMenu;
		private int _frameIndex;

		public AnimatedTrayIcon(ConfigViewModel sharedConfigVm, MainViewModel mainVm)
		{
			_sharedConfigVm = sharedConfigVm;
			_mainVm = mainVm;

			_notifyIcon = new NotifyIcon
			{
				Visible = true,
				Text = AppIdentity.AppDescription
			};

			_notifyIcon.MouseUp += NotifyIcon_MouseUp;

			LoadFrames();
			Log.Debug($"Frames: {_frames.Count}, Icons: {_icons.Count}");

			if (_frames.Count == 0)
				throw new InvalidOperationException("AnimatedTrayIcon: No frames found.");

			_notifyIcon.Icon = IconFromBitmap(_frames[0]);

			_timer = new DispatcherTimer
			{
				Interval = TimeSpan.FromMilliseconds(250)
			};
			_timer.Tick += OnTimerTick;
			_timer.Start();
		}

		private void OnTimerTick(object? sender, EventArgs e)
		{
			AdvanceFrame();
		}

		private async void NotifyIcon_MouseUp(object? sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Right)
			{
				await Task.Delay(100);
				ShowTrayMenu();
			}
			else if (e.Button == MouseButtons.Left)
			{
				System.Windows.Application.Current.Dispatcher.Invoke(() =>
				{
					_mainVm.TogglePopup();
				});
			}
		}

		private async void ShowTrayMenu()
		{
			await Task.Delay(50);

			// Bring invisible window to foreground
			App.HiddenWindow.Activate();

			_wpfMenu = BuildWpfMenu();

			// Tie the menu to the hidden window
			_wpfMenu.PlacementTarget = App.HiddenWindow;
			_wpfMenu.Placement = PlacementMode.MousePoint;
			_wpfMenu.StaysOpen = false;
			_wpfMenu.IsOpen = true;
		}

		// ------------------------------------------------------------
		// WPF MENU
		// ------------------------------------------------------------
		private ContextMenu BuildWpfMenu()
		{
			var menu = new ContextMenu();

			bool framesVisible = DebugIconWindow.IsOpen;
			bool countersVisible = _mainVm.PopupIsOpen;
			bool appIconVisible = _mainVm.ShowAppIcon;
			bool anyCounterVisible = _mainVm.Counters.Any(c => c.ShowInTray);

			// Configure Metrics
			menu.Items.Add(new MenuItem
			{
				Header = "Configure Metrics",
				Command = new RelayCommand(_ => _mainVm.ShowConfig())
			});

			menu.Items.Add(new Separator());

			// Icon Preview
			menu.Items.Add(new MenuItem
			{
				Header = framesVisible ? "Close IconSet Preview" : "Open IconSet Preview",
				Command = new RelayCommand(_ =>
				{
					if (framesVisible)
						DebugIconWindow.CloseAll();
					else
						new DebugIconWindow(IconSetConfig.IconSets.Keys.First()).Show();
				})
			});

			// Metrics View
			menu.Items.Add(new MenuItem
			{
				Header = countersVisible ? "Close Metrics View" : "Open Metrics View",
				Command = new RelayCommand(_ => _mainVm.TogglePopup())
			});

			menu.Items.Add(new Separator());

			// App Icon toggle
			menu.Items.Add(new MenuItem
			{
				Header = "Hide App Icon",
				IsEnabled = anyCounterVisible,   // <--- Grey out when no counters exist
				Command = new RelayCommand(_ =>
				{
					_mainVm.ToggleAppIcon();
				})
			});

			menu.Items.Add(new Separator());

			// Exit
			menu.Items.Add(new MenuItem
			{
				Header = "Exit",
				Command = new RelayCommand(_ =>
				{
					System.Windows.Application.Current.Shutdown();
				})
			});

			menu.Items.Add(new Separator());

			// About
			menu.Items.Add(new MenuItem
			{
				Header = "About",
				Command = new RelayCommand(_ =>
				{
					var existing = System.Windows.Application.Current.Windows
						.OfType<PerformanceTrayMonitor.Views.AboutWindow>()
						.FirstOrDefault();

					if (existing != null)
					{
						existing.Activate();
						return;
					}

					new PerformanceTrayMonitor.Views.AboutWindow().Show();
				})
			});

			menu.Opened += (_, __) => Log.Debug("WPF Menu: OPENED");
			menu.Closed += (_, __) => Log.Debug("WPF Menu: CLOSED");
			menu.PreviewKeyDown += (_, e) => Log.Debug($"WPF Menu: KEY {e.Key}");
			menu.PreviewMouseDown += (_, e) => Log.Debug($"WPF Menu: MOUSEDOWN {e.ChangedButton}");
			menu.PreviewMouseUp += (_, e) => Log.Debug($"WPF Menu: MOUSEUP {e.ChangedButton}");
			menu.PreviewMouseLeftButtonDown += (_, __) => Log.Debug("WPF Menu: LEFT DOWN");
			menu.PreviewMouseRightButtonDown += (_, __) => Log.Debug("WPF Menu: RIGHT DOWN");
			menu.PreviewMouseLeftButtonUp += (_, __) => Log.Debug("WPF Menu: LEFT UP");
			menu.PreviewMouseRightButtonUp += (_, __) => Log.Debug("WPF Menu: RIGHT UP");
			
			menu.PreviewKeyDown += (_, e) =>
			{
				Log.Debug($"WPF Menu: KEY = {e.Key}");
			};

			menu.PreviewMouseDown += (_, e) =>
			{
				Log.Debug($"WPF Menu: MOUSEDOWN = {e.ChangedButton}, Source={e.Source}");
			};

			menu.PreviewMouseUp += (_, e) =>
			{
				Log.Debug($"WPF Menu: MOUSEUP = {e.ChangedButton}, Source={e.Source}");
			};

			return menu;
		}

		// ------------------------------------------------------------
		// FRAME LOADING (EMBEDDED + OPTIONAL EXTERNAL)
		// ------------------------------------------------------------
		private void LoadFrames()
		{
			var externalPath = Path.Combine(
				AppContext.BaseDirectory,
				Paths.ExternalIconsRoot,
				Paths.AppIconsFolder,
				Paths.AppAnimatedFolder);

			if (Directory.Exists(externalPath))
			{
				var files = Directory.GetFiles(externalPath, "*.ico")
					.OrderBy(f => f)
					.ToList();

				if (files.Count > 0)
				{
					foreach (var file in files)
					{
						var bmp = new Icon(file).ToBitmap();
						_frames.Add(bmp);
						_icons.Add(IconFromBitmap(bmp));
					}

					return; // External overrides embedded
				}
			}

			LoadEmbeddedFrames();
		}

		private void LoadEmbeddedFrames()
		{
			_frames.Clear();

			string basePath = "/Resources/Icons/App/Animated/";

			string[] files =
			{
				"bubble-1.ico",
				"bubble-2.ico",
				"bubble-3.ico",
				"bubble-4.ico"
			};

			foreach (var file in files)
			{
				try
				{
					var bmp = LoadEmbeddedBitmap(basePath + file);
					_frames.Add(bmp);
					_icons.Add(IconFromBitmap(bmp));
				}
				catch (Exception ex)
				{
					Log.Debug($"Failed to load embedded icon {file}: {ex.Message}");
				}
			}

			if (_frames.Count == 0)
				throw new InvalidOperationException("AnimatedTrayIcon: No frames found.");
		}

		private Bitmap LoadEmbeddedBitmap(string relativePath)
		{
			var uri = new Uri(relativePath, UriKind.Relative);

			var info = System.Windows.Application.GetResourceStream(uri);
			if (info == null)
				throw new FileNotFoundException("Embedded icon not found: " + relativePath);

			using var ms = new MemoryStream();
			info.Stream.CopyTo(ms);
			ms.Position = 0;

			var bitmapFrame = BitmapFrame.Create(ms);
			return BitmapFromSource(bitmapFrame);
		}

		private static Bitmap BitmapFromSource(BitmapSource bitmapsource)
		{
			using var outStream = new MemoryStream();
			var encoder = new PngBitmapEncoder();
			encoder.Frames.Add(BitmapFrame.Create(bitmapsource));
			encoder.Save(outStream);
			outStream.Position = 0;
			return new Bitmap(outStream);
		}

		// ------------------------------------------------------------
		// ANIMATION
		// ------------------------------------------------------------
		private void AdvanceFrame()
		{
			if (_frames.Count == 0)
				return;

			_frameIndex = (_frameIndex + 1) % _frames.Count;
			_notifyIcon.Icon = _icons[_frameIndex];
		}

		private static Icon IconFromBitmap(Bitmap bmp)
		{
			IntPtr hIcon = bmp.GetHicon();

			var icon = Icon.FromHandle(hIcon);
			var clone = (Icon)icon.Clone();

			NativeMethods.DestroyIcon(hIcon);

			return clone;
		}

		internal static class NativeMethods
		{
			[System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
			public extern static bool DestroyIcon(IntPtr handle);
		}

		// ------------------------------------------------------------
		// DISPOSAL
		// ------------------------------------------------------------
		private bool _disposed;
		public void Dispose()
		{
			if (_disposed)
				return;

			_disposed = true;

			_timer.Stop();
			_timer.Tick -= OnTimerTick;

			_notifyIcon.Icon = null;
			_notifyIcon.Visible = false;
			_notifyIcon.MouseUp -= NotifyIcon_MouseUp;
			_notifyIcon.Dispose();

			foreach (var frame in _frames)
				frame.Dispose();
			_frames.Clear();

			foreach (var icon in _icons)
				icon.Dispose();
			_icons.Clear();

			GC.SuppressFinalize(this);
		}
	}
}
