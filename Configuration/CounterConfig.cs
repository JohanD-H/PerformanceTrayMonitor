using System.Collections.Generic;

namespace PerformanceTrayMonitor.Configuration
{
	internal static class CounterConfig
	{
		/// <summary>
		/// Supported display modes for counters.
		/// </summary>
		public static readonly IReadOnlyList<string> Modes =
			new[] { "Graph", "Activity", "Value" };

		/// <summary>
		/// Supported icon sets for tray icons.
		/// </summary>
		public static readonly IReadOnlyList<string> IconSets =
			new[] { "Activity", "Graphic" };

		public const string DefaultIconSet = "Activity";

		public const string DefaultCategory = "PhysicalDisk";
		public const string DefaultCounter = "% Disk Time";
		public const string DefaultInstance = "_Total";
		public const string DefaultDisplayName = "Disk Activity";
		public const float DefaultMin = 0;
		public const float DefaultMax = 100;
		public const string DefaultMode = "Activity";
	}
}
