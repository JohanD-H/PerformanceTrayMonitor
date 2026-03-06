using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace PerformanceTrayMonitor.Common
{
	public static class Log
	{
		public static ILogger Logger { get; set; }

		public static void Info(string message) =>
			Logger?.LogInformation(message);
		public static void Info(Exception ex, string message) =>
			Logger?.LogInformation(ex, message);

		public static void Debug(string message) =>
			Logger?.LogDebug(message);
		public static void Debug(Exception ex, string message) =>
			Logger?.LogDebug(ex, message);

		public static void Warning(string message) =>
			Logger?.LogWarning(message);
		public static void Warning(Exception ex, string message) =>
			Logger?.LogDebug(ex, message);
		public static void Error(string message) =>
			Logger?.LogError(message);
		public static void Error(Exception ex, string message) =>
				Logger?.LogDebug(ex, message);
	}
}
