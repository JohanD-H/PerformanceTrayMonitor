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

		// Maximum number of history values stored
		// Use for graphical display of the metric 
		public const int MaximumNumberOfHistoryValues = 60;

		// Animated Icon Set preview timer
		// Number of seconds between Icon Set updated in the Metric Configuration
		public const double IconSetPreviewTimer = 1.0;

		// Min and Max information text and header
		// Explains what the Min and Max values are used for.
		public const string MinMaxInformationText = "How Min and Max are used:\n\n" +
													"Line graph:\n" +
													"The small graph uses Min and Max to scale the line. " +
													"Values below Min appear at the bottom, values above Max at the top.\n\n" +
													"Animated icon (iconset):\n" +
													"If you use an animated icon, Min and Max determine which frame is shown. " +
													"The current value is mapped between Min and Max to pick the correct animation frame.";
		public const string MinMaxInformationHeader = "Min / Max Information";
	}
}
