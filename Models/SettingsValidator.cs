using PerformanceTrayMonitor.Configuration;
using System;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace PerformanceTrayMonitor.Models
{
	public static class SettingsValidator
	{
		public static void Validate(SettingsOptions options)
		{
			if (options == null)
				return;

			ValidateGlobal(options.Global);
			ValidateMetrics(options.Metrics);
			EnforceTrayLimit(options.Metrics);
		}

		// ---------------------------------------------------------
		// GLOBAL VALIDATION
		// ---------------------------------------------------------
		private static void ValidateGlobal(GlobalOptions global)
		{
			if (global == null)
				return;

			// Ensure CustomColors always has 16 entries
			if (global.CustomColors == null || global.CustomColors.Length != 16)
				global.CustomColors = new int[16];
		}

		// ---------------------------------------------------------
		// METRIC VALIDATION
		// ---------------------------------------------------------
		private static void ValidateMetrics(List<CounterSettings> metrics)
		{
			if (metrics == null)
				return;

			var defaults = new DefaultSettingsProvider().CreateDefaultCounter();
			var seenIds = new HashSet<Guid>();

			foreach (var m in metrics)
			{
				// Ensure ID uniqueness
				if (!seenIds.Add(m.Id))
					m.Id = Guid.NewGuid();

				// Min/Max normalization
				if (m.Min > m.Max)
					(m.Min, m.Max) = (m.Max, m.Min);

				// IconSet validation (name only) - leaving this to IconSetValidator!
				//if (!IconSetRegistry.ContainsKey(m.IconSet))
				//	m.IconSet = defaults.IconSet;

				// Color validation
				if (!IsValidColor(m.TrayAccentColor))
					m.TrayAccentColor = defaults.TrayAccentColor;

				if (!IsValidColor(m.TrayBackgroundColor))
					m.TrayBackgroundColor = defaults.TrayBackgroundColor;

				// DisplayName fallback
				if (string.IsNullOrWhiteSpace(m.DisplayName))
					m.DisplayName = GenerateDefaultName(m);
			}
		}

		// ---------------------------------------------------------
		// TRAY LIMIT ENFORCEMENT
		// ---------------------------------------------------------
		private static void EnforceTrayLimit(List<CounterSettings> metrics)
		{
			if (metrics == null)
				return;

			var trayMetrics = metrics.Where(m => m.ShowInTray).ToList();

			if (trayMetrics.Count <= TrayIconConfig.MaxCounterTrayIcons)
				return;

			// Disable extra metrics beyond the allowed limit
			foreach (var extra in trayMetrics.Skip(TrayIconConfig.MaxCounterTrayIcons))
				extra.ShowInTray = false;
		}

		// ---------------------------------------------------------
		// HELPERS
		// ---------------------------------------------------------
		private static bool IsValidColor(Color c)
		{
			// Reject empty, transparent, NaN, or invalid ARGB
			return c.A != 0 || (c.R != 0 || c.G != 0 || c.B != 0);
		}

		private static string GenerateDefaultName(CounterSettings m)
		{
			// Simple fallback naming
			if (!string.IsNullOrWhiteSpace(m.Counter))
				return m.Counter;

			return "Metric";
		}
	}
}
