using PerformanceTrayMonitor.Common;
using PerformanceTrayMonitor.Configuration;
using PerformanceTrayMonitor.Models;
using PerformanceTrayMonitor.ViewModels;
using System;
using System.Windows;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Controls;
using System.Windows.Forms;

// --------------------------------------------
// Performance Counter tray icon animation
// --------------------------------------------

namespace PerformanceTrayMonitor.Tray
{
	public sealed class CounterTrayIcon : IDisposable
	{
		private readonly CounterSettings _settings;
		private readonly Func<double> _valueProvider;
		private readonly Icon[] _frames;

		private readonly NotifyIcon _notifyIcon;
		private readonly Timer _updateTimer;
		private readonly MainViewModel _mainVm;

		public System.Windows.Media.Color TrayBackgroundColor { get; set; }
		private Icon? _lastClone;

		// ------------------------------------------------------------
		// BUILD THE COUNTER TRAY ICON AND GIVE IT MOUSE HANDLING!
		// ------------------------------------------------------------
		public CounterTrayIcon(CounterSettings settings, Func<double> valueProvider, IconSetDefinition set, MainViewModel mainVm)
		{
			_mainVm = mainVm;
			_settings = settings;
			_valueProvider = valueProvider;

			_frames = LoadFrames(set);

			if (_frames.Length == 0)
			{
				Log.Error($"Icon set '{set.Name}' contains no frames. Using emergency fallback icon.");
				_frames = new[] { SystemIcons.Warning }; // or your own embedded fallback icon
			}

			_notifyIcon = new NotifyIcon
			{
				Icon = _frames[0],
				Visible = true,
				Text = settings.DisplayName
			};

			_notifyIcon.MouseUp += (s, e) =>
			{
				System.Windows.Application.Current.Dispatcher.Invoke(() =>
				{
					if (e.Button == MouseButtons.Left)
					{
						_mainVm.TogglePopup();
					}
				});
			};

			_updateTimer = new Timer { Interval = TrayIconConfig.AnimatedCounterUpdateTimerValue };
			_updateTimer.Tick += (_, _) => UpdateIcon();
			_updateTimer.Start();
		}

		// ------------------------------------------------------------
		// FRAME LOADING (EMBEDDED + EXTERNAL)
		// ------------------------------------------------------------
		private static Icon[] LoadFrames(IconSetDefinition set)
		{
			var icons = new List<Icon>();

			foreach (var frame in set.Frames)
			{
				try
				{
					if (set.IsEmbedded)
					{
						// Embedded icon via pack URI
						var uri = new Uri(frame, UriKind.Absolute);
						var streamInfo = System.Windows.Application.GetResourceStream(uri);

						if (streamInfo == null)
						{
							Log.Error($"Embedded icon not found: {frame}");
							continue;
						}

						using var s = streamInfo.Stream;
						icons.Add(new Icon(s));
					}
					else
					{
						// External icon on disk
						var localPath = new Uri(frame, UriKind.Absolute).LocalPath;
						using var fs = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
						icons.Add(new Icon(fs));
					}
				}
				catch (Exception ex)
				{
					Log.Error($"{ex} Failed to load icon frame '{frame}' for set '{set.Name}'.");
				}
			}

			return icons.ToArray();
		}

		// ------------------------------------------------------------
		// TOOLTIP
		// ------------------------------------------------------------
		private string BuildTooltip(double value)
		{
			bool isPercent =
				(_settings.Min == 0 && _settings.Max == 100) ||
				_settings.Counter.TrimStart().StartsWith("%");

			string valuePart = isPercent
				? $"{value:0}%"
				: FormatAsBytesOrRaw(value);

			string instancePart = string.IsNullOrWhiteSpace(_settings.Instance)
				? ""
				: $" ({_settings.Instance})";

			string tooltip =
				$"{_settings.DisplayName}{Environment.NewLine}" +
				$"{valuePart}{Environment.NewLine}" +
				$"{_settings.Category}/{_settings.Counter}{instancePart}";

			if (tooltip.Length > 63)
				tooltip = tooltip.Substring(0, 63);

			return tooltip;
		}

		private static string FormatAsBytesOrRaw(double value)
		{
			if (value > 1_000_000_000)
				return $"{value / 1_000_000_000:0.0} GB/s";

			if (value > 1_000_000)
				return $"{value / 1_000_000:0.0} MB/s";

			if (value > 1_000)
				return $"{value / 1_000:0.0} KB/s";

			return $"{value:0}";
		}

		private string FormatValueForTray(double value)
		{
			// Percent counters
			if ((_settings.Min == 0 && _settings.Max == 100) ||
				_settings.Counter.TrimStart().StartsWith("%"))
			{
				return $"{value:0}";
			}

			// Raw numbers
			if (value < 10)
				return $"{value:0.0}";

			if (value < 100)
				return $"{value:0}";

			return $"{value:0}";
		}

		// ------------------------------------------------------------
		// UPDATE LOOP
		// ------------------------------------------------------------
		private int _lastFrameIndex = -1;
		//private double _old_value = 0.0;

		private void UpdateIcon()
		{
			double value = _valueProvider();

			// Always update tooltip
			_notifyIcon.Text = BuildTooltip(value);

			// If using text mode, skip animation entirely
			if (_settings.UseTextTrayIcon)
			{
				UpdateTextIcon(value);
				return;
			}

			// Otherwise use animated frames
			// Old GetFrameIndex move to more generic place
			//int index = GetFrameIndex(value, _settings.Min, _settings.Max, _frames.Length);
			int index = TrayIconGenerator.GetFrameIndex(
				value,
				_settings.Min,
				_settings.Max,
				_frames.Length
			);


#if DEBUGx
			// To trap on anything "Network" use:
			// if (_settings.Category?.Contains("Network", StringComparison.OrdinalIgnoreCase) == true && value != 0)
			// The below simply traps on a specific category, "Network Interface" in this case
			if (string.Equals(_settings.Category, "Network Interface", StringComparison.OrdinalIgnoreCase) &&
				value != 0 &&
				value != _old_value)
			{
				_old_value = value;
				Log.Debug($"[Network] Raw value = {value}, index = {index}");
			}
#endif
			if (index != _lastFrameIndex)
			{
				_lastFrameIndex = index;

				// Dispose the previous clone (if any)
				_lastClone?.Dispose();

				// Create a new clone and assign it
				_lastClone = (Icon)_frames[index].Clone();
				_notifyIcon.Icon = _lastClone;
			}
		}

		private void UpdateTextIcon(double value)
		{
			string text = FormatValueForTray(value);

			// Convert WPF colors → Drawing.Color
			var accent = _settings.TrayAccentColor.ToDrawingColor();

			var bg = _settings.AutoTrayBackground
				? UIColors.GetTrayBackground(accent, autoContrast: true)
				: _settings.TrayBackgroundColor.ToDrawingColor();

			// DPI scale: ask WinForms for the current device DPI
			float dpi = _notifyIcon.Icon?.Size.Height ?? 16;
			double dpiScale = dpi / 16.0;

			Icon newIcon = TrayIconGenerator.CreateTextIcon(
				text,
				accent,
				bg,
				dpiScale);

			// Store the old icon clone for Disposal
			var oldIcon = _notifyIcon.Icon;

			// Assign the new clone
			_notifyIcon.Icon = newIcon;

			// Dispose only the old clone
			oldIcon?.Dispose();
		}

		public void UpdateContextMenu()
		{
			if (_mainVm.ShowAppIcon)
			{
				// App icon is visible → counters get NO menu
				_notifyIcon.ContextMenuStrip = null;
			}
			else
			{
				// App icon hidden → counters get the tiny menu
				var wpfMenu = BuildCounterMenu();

				// Convert WPF ContextMenu → WinForms ContextMenuStrip
				var cms = new ContextMenuStrip();

				foreach (var item in wpfMenu.Items)
				{
					if (item is MenuItem mi)
					{
						cms.Items.Add(mi.Header.ToString(), null, (_, _) =>
						{
							mi.Command?.Execute(null);
						});
					}
					else if (item is Separator)
					{
						cms.Items.Add(new ToolStripSeparator());
					}
				}

				_notifyIcon.ContextMenuStrip = cms;
			}
		}

		private ContextMenu BuildCounterMenu()
		{
			var menu = new ContextMenu();

			// Show App Icon
			menu.Items.Add(new MenuItem
			{
				Header = "Show App Icon",
				Command = new RelayCommand(_ => _mainVm.ShowAppIconExplicit())
			});

			// Separator for safety spacing
			menu.Items.Add(new Separator());

			// Exit
			menu.Items.Add(new MenuItem
			{
				Header = "Exit",
				Command = new RelayCommand(_ => System.Windows.Application.Current.Shutdown())
			});

			return menu;
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

			_updateTimer.Stop();
			_updateTimer.Dispose();

			_notifyIcon.Icon = null;     // Prevent rare WinForms handle reuse issues
			_notifyIcon.Text = "";
			_notifyIcon.Visible = false;
			_notifyIcon.Dispose();

			foreach (var icon in _frames)
				icon.Dispose();
		}
	}
}
