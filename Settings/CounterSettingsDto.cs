using System;
using System.Windows;

namespace PerformanceTrayMonitor.Settings
{
	public sealed class CounterSettingsDto
	{
		public Guid Id { get; set; }
		public string Category { get; set; } = "";
		public string Counter { get; set; } = "";
		public string Instance { get; set; } = "";
		public string DisplayName { get; set; } = "";
		public float Min { get; set; }
		public float Max { get; set; }
		public bool ShowInTray { get; set; }
		public string IconSet { get; set; } = "Activity";

		public bool UseTextTrayIcon { get; set; }
		public int TrayAccentColorArgb { get; set; }
		public bool AutoTrayBackground { get; set; }
		public int TrayBackgroundColorArgb { get; set; }

		public bool IsEquivalentToDefault(CounterSettingsDto other)
		{
			return
				string.Equals(DisplayName, other.DisplayName, StringComparison.Ordinal) &&
				string.Equals(Category, other.Category, StringComparison.Ordinal) &&
				string.Equals(Counter, other.Counter, StringComparison.Ordinal) &&
				string.Equals(Instance, other.Instance, StringComparison.Ordinal) &&
				Min == other.Min &&
				Max == other.Max &&
				ShowInTray == other.ShowInTray;
		}
	}
}
