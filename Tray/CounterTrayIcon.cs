using PerformanceTrayMonitor.Configuration;
using PerformanceTrayMonitor.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;

namespace PerformanceTrayMonitor.Tray
{
	public sealed class CounterTrayIcon : IDisposable
	{
		private readonly CounterSettings _settings;
		private readonly Func<double> _valueProvider;
		private readonly Icon[] _frames;

		private readonly NotifyIcon _notifyIcon;
		private readonly Timer _updateTimer;

		public CounterTrayIcon(CounterSettings settings, Func<double> valueProvider, IconSetDefinition set)
		{
			_settings = settings;
			_valueProvider = valueProvider;

			Log.Debug($"CounterTrayIcon created for '{settings.DisplayName}' using set '{set.Name}'.");

			_frames = LoadFrames(set);

			if (_frames.Length == 0)
				throw new InvalidOperationException($"Icon set '{set.Name}' contains no frames.");

			_notifyIcon = new NotifyIcon
			{
				Icon = _frames[0],
				Visible = true,
				Text = settings.DisplayName
			};

			_updateTimer = new Timer { Interval = 500 };
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
						using var fs = File.OpenRead(new Uri(uri).LocalPath);
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
				$"{_settings.DisplayName}\n" +
				$"{valuePart}\n" +
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
		private void UpdateIcon()
		{
			double value = _valueProvider();

			int index = GetFrameIndex(value, _settings.Min, _settings.Max, _frames.Length);
			_notifyIcon.Icon = _frames[index];

			_notifyIcon.Text = BuildTooltip(value);
		}

		private static int GetFrameIndex(double value, double min, double max, int frameCount)
		{
			value = Math.Max(min, Math.Min(max, value));

			double normalized = (value - min) / (max - min);

			int index = (int)(normalized * frameCount);
			return Math.Max(0, Math.Min(frameCount - 1, index));
		}

		// ------------------------------------------------------------
		// DISPOSAL
		// ------------------------------------------------------------
		public void Dispose()
		{
			_updateTimer.Stop();
			_updateTimer.Dispose();

			_notifyIcon.Visible = false;
			_notifyIcon.Dispose();

			foreach (var icon in _frames)
				icon.Dispose();

			Log.Debug($"CounterTrayIcon disposed for '{_settings.DisplayName}'.");
		}
	}
}
