using System;
using System.IO;

// -------------------------------------------
// Application path data
// -------------------------------------------
namespace PerformanceTrayMonitor.Configuration
{
	internal static class Paths
	{
		internal static readonly string AppDirectory =
			AppContext.BaseDirectory;

		internal static readonly string SettingsFile =
			Path.Combine(AppDirectory, $"{AppIdentity.AppId}.json");

		internal static readonly string BaseDirectory =
			new DirectoryInfo(AppDirectory).Parent.Parent.Parent.ToString();

		internal static readonly string LogsDirectory =
			Path.Combine(BaseDirectory, "Logs");

		internal static readonly string TempDirectory =
			Path.Combine(AppDirectory, "Temp");

		// Base folder for external icons (relative to executable)
		public const string ExternalIconsRoot = "Icons";

		// Subfolders for app icons
		public const string AppIconsFolder = "App";
		public const string AppAnimatedFolder = "Animated";
		public const string AppStaticIcon = "App.ico";

		// Subfolders for counter icons
		public const string CounterIconsFolder = "Counter";

		// Embedded resource root (mirrors external structure)
		public const string EmbeddedIconsRoot = "Resources/Icons";

	}
}
