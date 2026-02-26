using PerformanceTrayMonitor.Debugging;
using PerformanceTrayMonitor.ViewModels;
using PerformanceTrayMonitor.Views;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PerformanceTrayMonitor.Configuration
{
	internal sealed class AnimatedTrayIcon : IDisposable
	{
		private readonly NotifyIcon _notifyIcon;
		private readonly DispatcherTimer _timer;
		private readonly List<Bitmap> _frames = new();
		private readonly ContextMenuStrip _menu = new();
		private readonly ConfigViewModel _configVm;
		private readonly MainViewModel _mainVm; // ← add this

		private int _frameIndex;

		public AnimatedTrayIcon(ConfigViewModel configVm, MainViewModel mainVm)
		{
			_configVm = configVm;
			_mainVm = mainVm;

			_notifyIcon = new NotifyIcon
			{
				Visible = true,
				Text = AppIdentity.AppDescription
			};

			BuildMenu();

			_notifyIcon.MouseUp += (s, e) =>
			{
				if (e.Button == MouseButtons.Right)
				{
					BuildMenu();
					_menu.Show(Cursor.Position);
				}
				if (e.Button == MouseButtons.Left)
				{
					System.Windows.Application.Current.Dispatcher.Invoke(() =>
					{
						_mainVm.TogglePopup();
					});
				}
			};

			LoadFrames();

			if (_frames.Count == 0)
				throw new InvalidOperationException("AnimatedTrayIcon: No frames found.");

			_notifyIcon.Icon = IconFromBitmap(_frames[0]);

			_timer = new DispatcherTimer
			{
				Interval = TimeSpan.FromMilliseconds(250)
			};
			_timer.Tick += (_, _) => AdvanceFrame();
			_timer.Start();
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
						_frames.Add(new Icon(file).ToBitmap());

					return; // External overrides embedded
				}
			}

			LoadEmbeddedFrames();
		}

		/*
		 * The below needs improving;
		 * Automatic discovery of the *.ico files
		 * Moving basePath into Paths.cs
		 */
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
			_notifyIcon.Icon = IconFromBitmap(_frames[_frameIndex]);
		}

		private static Icon IconFromBitmap(Bitmap bmp)
		{
			IntPtr hIcon = bmp.GetHicon();
			return Icon.FromHandle(hIcon);
		}

		// ------------------------------------------------------------
		// MENU
		// ------------------------------------------------------------
		private void BuildMenu()
		{
			bool framesVisible = DebugIconWindow.IsOpen;   // you can expose a static flag
			bool countersVisible = _mainVm.IsPopupOpen;    // you already track this

			_menu.Items.Clear();

			_menu.Items.Add(framesVisible ? "Hide Frames" : "Show Frames", null, (_, _) =>
			{
				System.Windows.Application.Current.Dispatcher.Invoke(() =>
				{
					if (framesVisible)
						DebugIconWindow.CloseAll();
					else
						new DebugIconWindow().Show();
				});
			});

			_menu.Items.Add(countersVisible ? "Hide Counters" : "Show Counters", null, (_, _) =>
			{
				System.Windows.Application.Current.Dispatcher.Invoke(() =>
				{
					_mainVm.TogglePopup();
				});
			});

			_menu.Items.Add("Configuration", null, (_, _) =>
			{
				System.Windows.Application.Current.Dispatcher.Invoke(() =>
				{
					var settings = new ConfigWindow(_configVm);
					settings.Show();
					settings.Activate();
				});
			});

			_menu.Items.Add(new ToolStripSeparator());

			_menu.Items.Add("About", null, (_, _) =>
			{
				System.Windows.Application.Current.Dispatcher.Invoke(() =>
				{
					// 1. Check if an instance is already open
					var existing = System.Windows.Application.Current.Windows
						.OfType<PerformanceTrayMonitor.Views.AboutWindow>()
						.FirstOrDefault();

					if (existing != null)
					{
						existing.Activate();
						return;
					}

					// 2. Create the instance (This was the missing piece!)
					var about = new PerformanceTrayMonitor.Views.AboutWindow();

					// 3. Set the Owner
					// Note: Application.Current.Windows is a collection, not a single window.
					// Usually, you want the MainWindow as the owner.
					var main = System.Windows.Application.Current.MainWindow;
					if (main != null && main != about)
					{
						about.Owner = main;
					}

					// 4. Show the window
					about.ShowDialog();
				});
			});
			/*
			_menu.Items.Add("About", null, (_, _) =>
			{
				System.Windows.Application.Current.Dispatcher.Invoke(() =>
				{
					var existing = System.Windows.Application.Current.Windows
						.OfType<PerformanceTrayMonitor.Views.AboutWindow>()
						.FirstOrDefault();

					if (existing != null)
					{
						existing.Activate();
						return;
					}

					var main = System.Windows.Application.Current.Windows;
					//var main = Application.Current.MainWindow;
					if (main != null && main != about)
						about.Owner = main;

					about.ShowDialog();
				});
			});
			*/

			_menu.Items.Add(new ToolStripSeparator());


			_menu.Items.Add("Exit", null, (_, _) =>
			{
				// 1. Close the WinForms menu FIRST
				_menu.Close();

				// 2. Shutdown WPF AFTER the menu is gone
				System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
				{
					System.Windows.Application.Current.Shutdown();
				}));
			});
		}

		// ------------------------------------------------------------
		// DISPOSAL
		// ------------------------------------------------------------
		public void Dispose()
		{
			_timer.Stop();
			_notifyIcon.Visible = false;
			_notifyIcon.Dispose();

			foreach (var frame in _frames)
				frame.Dispose();
		}
	}
}
