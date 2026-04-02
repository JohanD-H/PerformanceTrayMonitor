using Microsoft.Extensions.Logging;
using System;
using System.Windows;

namespace PerformanceTrayMonitor.Common
{
	public static class Log
	{
		public static ILogger? Logger { get; set; }

		public static void Debug(string message)
		{
			Logger?.LogDebug(message);
			System.Diagnostics.Debug.WriteLine(message);               // VS Output
			System.Diagnostics.Trace.WriteLine(message);               // DebugView
		}

		public static void Debug(Exception ex, string message)
		{
			Logger?.LogDebug(ex, message);
			System.Diagnostics.Debug.WriteLine($"{message}: {ex}");
			System.Diagnostics.Trace.WriteLine($"{message}: {ex}");
		}

		public static void Info(string message)
		{
			Logger?.LogInformation(message);
			System.Diagnostics.Debug.WriteLine("INFO: " + message);
			System.Diagnostics.Trace.WriteLine("INFO: " + message);
		}

		public static void Info(Exception ex, string message)
		{
			Logger?.LogInformation(ex, message);
			System.Diagnostics.Debug.WriteLine($"INFO: {message}: {ex}");
			System.Diagnostics.Trace.WriteLine($"INFO: {message}: {ex}");
		}

		public static void Warning(string message)
		{
			Logger?.LogWarning(message);
			System.Diagnostics.Debug.WriteLine("WARNING: " + message);
			System.Diagnostics.Trace.WriteLine("WARNING: " + message);
		}

		public static void Warning(Exception ex, string message)
		{
			Logger?.LogWarning(ex, message);
			System.Diagnostics.Debug.WriteLine($"WARNING: {message}: {ex}");
			System.Diagnostics.Trace.WriteLine($"WARNING: {message}: {ex}");
		}

		public static void Error(string message)
		{
			Logger?.LogError(message);
			System.Diagnostics.Debug.WriteLine("ERROR: " + message);
			System.Diagnostics.Trace.WriteLine("ERROR: " + message);
		}

		public static void Error(Exception ex, string message)
		{
			Logger?.LogError(ex, message);
			System.Diagnostics.Debug.WriteLine($"ERROR: {message}: {ex}");
			System.Diagnostics.Trace.WriteLine($"ERROR: {message}: {ex}");
		}
	}
}
