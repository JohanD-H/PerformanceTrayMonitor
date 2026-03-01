using PerformanceTrayMonitor.Configuration;
using PerformanceTrayMonitor.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

// ------------------------------------------
// Specific application data
// ------------------------------------------
namespace PerformanceTrayMonitor.Configuration
{
	internal static class AppIdentity
	{
		// This NEVER changes, even if the assembly name does.
		internal const string AppId = "PerformanceTrayMonitor";

		public const string AppName = "Performance Tray Monitor";
		public const string AppDescription = "Performance Tray Monitor";
		public const string AppVersion = "1.0.0"; // optional
	}
}
