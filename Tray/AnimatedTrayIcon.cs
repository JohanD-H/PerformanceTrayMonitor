using PerformanceTrayMonitor.Debugging;
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
		private readonly ContextMenuStrip _menu = new();
		private readonly ConfigViewModel _sharedConfigVm;
		private readonly MainViewModel _mainVm;
		private readonly List<Icon> _icons = new();

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

		private void NotifyIcon_MouseUp(object? sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Right)
			{
				BuildMenu();
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
			// Let Windows settle the tray icon position
			await Task.Delay(50);
			// Put the context menu by the cursor
			_menu.Show(Cursor.Position);
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

			// Relative pack-URI paths (required for GetResourceStream)
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
			// Must be a RELATIVE URI
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

			// Wrap the handle
			var icon = Icon.FromHandle(hIcon);

			// Clone to detach from the raw handle
			var clone = (Icon)icon.Clone();

			// Now it's safe to destroy the original HICON
			NativeMethods.DestroyIcon(hIcon);

			return clone;
		}

		internal static class NativeMethods
		{
			[System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
			public extern static bool DestroyIcon(IntPtr handle);
		}

		// ------------------------------------------------------------
		// MENU
		// ------------------------------------------------------------
		private void BuildMenu()
		{
			bool framesVisible = DebugIconWindow.IsOpen;   // you can expose a static flag
			bool countersVisible = _mainVm.PopupIsOpen;    // you already track this
			bool appIconVisible = _mainVm.ShowAppIcon;
			bool anyCounterVisible = _mainVm.Counters.Any(c => c.ShowInTray);

			_menu.Items.Clear();

			// Put the configuration option on top, it's the most often used
			_menu.Items.Add("Configure Metrics", null, (_, _) =>
			{
				System.Windows.Application.Current.Dispatcher.Invoke(() =>
				{
					_mainVm.ShowConfig();
				});
			});

			_menu.Items.Add(new ToolStripSeparator());

			_menu.Items.Add(framesVisible ? "Close Icon Preview" : "Open Icon Preview", null, (_, _) =>
			{
				System.Windows.Application.Current.Dispatcher.Invoke(() =>
				{
					if (framesVisible)
						DebugIconWindow.CloseAll();
					else
						new DebugIconWindow().Show();
				});
			});

			_menu.Items.Add(countersVisible ? "Close Metrics View" : "Open Metrics View", null, (_, _) =>
			{
				System.Windows.Application.Current.Dispatcher.Invoke(() =>
				{
					_mainVm.TogglePopup();
				});
			});

			_menu.Items.Add(new ToolStripSeparator());

			_menu.Items.Add(
				appIconVisible ? "Hide App Icon" : "Show App Icon",
				null,
				(_, _) =>
				{
					System.Windows.Application.Current.Dispatcher.Invoke(() =>
					{
						if (!appIconVisible && !anyCounterVisible)
						{
							MessageBox.Show("You must have at least one tray icon visible.", "Warning");
							return;
						}

						_mainVm.ToggleAppIcon();
						BuildMenu();
					});
				});

			_menu.Items.Add(new ToolStripSeparator());

			// It is custom to put exit low in the menu.
			_menu.Items.Add("Exit", null, (_, _) =>
			{
				// Close the WinForms menu FIRST
				_menu.Close();

				// Shutdown WPF AFTER the menu is gone
				System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
				{
					System.Windows.Application.Current.Shutdown();
				}));
			});

			_menu.Items.Add(new ToolStripSeparator());

			// The About on the bottom, least used item
			_menu.Items.Add("About", null, (_, _) =>
			{
				System.Windows.Application.Current.Dispatcher.Invoke(() =>
				{
					// Check if an instance is already open
					var existing = System.Windows.Application.Current.Windows
						.OfType<PerformanceTrayMonitor.Views.AboutWindow>()
						.FirstOrDefault();

					if (existing != null)
					{
						existing.Activate();
						return;
					}

					// Create the instance
					var about = new PerformanceTrayMonitor.Views.AboutWindow();

					// Show modeless (fixes centering + transparency + animation)
					about.Show();
				});
			});
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
			//_timer.Dispose();

			_menu.Items.Clear();
			_menu.Close();
			_menu.Dispose();
			// _menu = null;         // would require removing readonly


			_notifyIcon.Icon = null;
			_notifyIcon.Visible = false;
			_notifyIcon.MouseUp -= NotifyIcon_MouseUp;
			_notifyIcon.Dispose();
			// _notifyIcon = null;   // would require removing readonly

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
