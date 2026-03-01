using PerformanceTrayMonitor.Configuration;
using PerformanceTrayMonitor.Models;
using PerformanceTrayMonitor.ViewModels;
using Serilog;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows;
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

		// ------------------------------------------------------------
		// BUILD THE COUNTER TRAY ICON AND GIVE IT MOUSE HANDLING!
		// ------------------------------------------------------------
		public CounterTrayIcon(CounterSettings settings, Func<double> valueProvider, IconSetDefinition set, MainViewModel mainVm)
		{
			_mainVm = mainVm;
			_settings = settings;
			_valueProvider = valueProvider;

			Log.Debug($"CounterTrayIcon created for '{settings.DisplayName}' using set '{set.Name}'.");

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
					else if (e.Button == MouseButtons.Right)
					{
						if (!_mainVm.ShowAppIcon)
							_mainVm.ToggleAppIcon();
					}
				});
			};

			_updateTimer = new Timer { Interval = 50 };
			_updateTimer.Tick += (_, _) => UpdateIcon();
			_updateTimer.Start();
		}

		// ------------------------------------------------------------
		// FRAME LOADING (EMBEDDED + EXTERNAL)
		// ------------------------------------------------------------
		private Icon[] LoadFrames(IconSetDefinition set)
		{
			var icons = new List<Icon>();

			foreach (var uri in set.Frames)
			{
				try
				{
					if (uri.StartsWith("/"))
					{
						// Embedded WPF Resource (relative pack URI)
						var resourceUri = new Uri(uri, UriKind.Relative);
						var streamInfo = System.Windows.Application.GetResourceStream(resourceUri);

						if (streamInfo != null)
						{
							using var s = streamInfo.Stream;
							icons.Add(new Icon(s));
						}
						else
						{
							Log.Error($"Embedded icon not found: {uri}");
						}
					}
					else
					{
						// External file
						var localPath = new Uri(uri).LocalPath;
						using var fs = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
						icons.Add(new Icon(fs));
					}
				}
				catch (Exception ex)
				{
					Log.Error(ex, $"Failed to load icon frame '{uri}' for set '{set.Name}'.");
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

		private string FormatAsBytesOrRaw(double value)
		{
			if (value > 1_000_000_000)
				return $"{value / 1_000_000_000:0.0} GB/s";

			if (value > 1_000_000)
				return $"{value / 1_000_000:0.0} MB/s";

			if (value > 1_000)
				return $"{value / 1_000:0.0} KB/s";

			return $"{value:0}";
		}

		// ------------------------------------------------------------
		// UPDATE LOOP
		// ------------------------------------------------------------
		private int _lastFrameIndex = -1;

		private void UpdateIcon()
		{
			double value = _valueProvider();

			int index = GetFrameIndex(value, _settings.Min, _settings.Max, _frames.Length);

			// To trap on anything "Network" use:
			// if (_settings.Category?.Contains("Network", StringComparison.OrdinalIgnoreCase) == true && value != 0)
			// The below simply traps on a specific category, "Network Interface" in this case
			if (string.Equals(_settings.Category, "Network Interface", StringComparison.OrdinalIgnoreCase) && value != 0)
			{
				Log.Debug($"[Network] Raw value = {value}, index = {index}");
			}

			// Only update the icon if the frame actually changed
			if (index != _lastFrameIndex)
			{
			_lastFrameIndex = index;
				_notifyIcon.Icon = _frames[index];
			}

			// Tooltip can update every tick; it's cheap
			_notifyIcon.Text = BuildTooltip(value);
		}

		private static int GetFrameIndex(double value, double min, double max, int frameCount)
		{
			double val = Math.Max(min, Math.Min(max, value));
			//Log.Debug($"val = {val}");

			double normalized = (val - min) / (max - min);
			//Log.Debug($"normalized = {normalized}");

			// Below both work, but pick what feels best.
			//
			// Normalize, standard
			// int index = (int)(normalized * (frameCount - 1));
			// Normalize, but gives a smoother transition
			int index = (int)Math.Round(normalized * (frameCount - 1));

			return Math.Max(0, Math.Min(frameCount - 1, index));
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

			Log.Debug($"CounterTrayIcon disposed for '{_settings.DisplayName}'.");
		}
	}
}
