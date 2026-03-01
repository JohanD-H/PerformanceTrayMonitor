using System;
using System.IO;
using System.Text.Json;

// -------------------------------------------
// Configuration data
// -------------------------------------------
namespace PerformanceTrayMonitor.Configuration
{
	internal static class Config
	{
		internal static string EnvironmentName =>
			Environment.GetEnvironmentVariable("PTM_ENV") ?? "Production";

		internal static bool IsDevelopment =>
			EnvironmentName.Equals("Development", StringComparison.OrdinalIgnoreCase);

		internal static bool IsDebug =>
#if DEBUG
			true;
#else
            false;
#endif
	}
}
