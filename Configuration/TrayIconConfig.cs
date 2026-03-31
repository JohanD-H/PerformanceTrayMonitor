using System;
using System.Windows;

// -------------------------------------------
// Tray icon settimgs
// -------------------------------------------
namespace PerformanceTrayMonitor.Configuration
{
	internal static class TrayIconConfig
	{
		/// Maximum number of counter tray icons the user may enable.
		/// Does NOT include the main animated app icon set!.
		public const int MaxCounterTrayIcons = 4;

		/// Maximum number of Embedded tray icons per Icon set.
		/// Does NOT include the main animated app icon set!.
		public const int MaxEmbeddedIconsSetIcons = 10;

		/// Maximum number of External tray icons per Icon set.
		/// Does NOT include the main animated app icon set!.
		public const int MaxExternalIconsSetIcons = 10;

		// Minimum and Maximun icons in a Icon Set.
		// Is NOT valid for the main animated app icon set!
		public const int MinIconSet = 2;
		public const int MaxIconSet = 10;

		// Animated Counter timer update value.
		// Number of milliseconds between tray icon updates
		public const int AnimatedCounterUpdateTimerValue = 50;

		// Animated App timer update value
		// Number of milliseconds between tray icon updates
		public const double AnimatedAppUpdateTimerValue = 250.0;

	}
}
