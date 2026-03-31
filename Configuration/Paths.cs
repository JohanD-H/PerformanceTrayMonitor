using System;
using System.Windows;
using System.IO;

namespace PerformanceTrayMonitor.Configuration
{
	// -----------------------------------------------------------
	// Centralized application paths
	// -----------------------------------------------------------
	internal static class Paths
	{
		// Root directory of the running application
		internal static readonly string AppDirectory =
			AppContext.BaseDirectory;

		// Settings file stored next to the executable
		internal static readonly string SettingsFile =
			Path.Combine(AppDirectory, $"{AppIdentity.AppId}.json");

		// Base directory of the project (three levels up from /bin/)
		//internal static readonly string BaseDirectory =
		//	new DirectoryInfo(AppDirectory).Parent.Parent.Parent.ToString();
		internal static readonly string BaseDirectory = AppDirectory.ToString();

		// -------------------------------------------------------
		// Icon folder structure
		// -------------------------------------------------------

		// External icon root (relative to executable)
		public const string ExternalIconsRoot = "Icons";

		// Subfolder for app tray icons
		public const string AppIconsFolder = "App";

		// Subfolder for animated app icons
		public const string AppAnimatedFolder = "Animated";

		// Default static app icon
		public const string AppStaticIcon = "App.ico";

		// Subfolder for counter icon sets
		public const string CounterIconsFolder = "Counter";

		// Embedded resource root (mirrors external structure)
		public const string EmbeddedIconsRoot = "Resources/Icons";
	}
}
