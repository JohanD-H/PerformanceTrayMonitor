using Microsoft.Extensions.Logging;
using System;
using System.Reflection;
using System.Windows;
using System.Runtime.CompilerServices;

namespace PerformanceTrayMonitor.Common
{
	public static class Log
	{
		public static ILogger? Logger { get; set; }

		public static void Debug(string message, [CallerMemberName] string methodname = "")
		{
			Logger?.LogDebug("[{Method}] {Message}", methodname, message);
			System.Diagnostics.Debug.WriteLine($"[{methodname}] {message}");
			System.Diagnostics.Trace.WriteLine($"[{methodname}] {message}");
		}

		public static void Debug(Exception ex, string message, [CallerMemberName] string methodname = "")
		{
			Logger?.LogDebug(ex, "[{Method}] {Message}", methodname, message);
			System.Diagnostics.Debug.WriteLine($"[{methodname}] {message}: {ex}");
			System.Diagnostics.Trace.WriteLine($"[{methodname}] {message}: {ex}");
		}

		public static void Info(string message, [CallerMemberName] string methodname = "")
		{
			Logger?.LogInformation("[{Method}] {Message}", methodname, message);
			System.Diagnostics.Debug.WriteLine($"[{methodname}] INFO: {message}");
			System.Diagnostics.Trace.WriteLine($"[{methodname}] INFO: {message}");
		}

		public static void Info(Exception ex, string message, [CallerMemberName] string methodname = "")
		{
			Logger?.LogInformation(ex, "[{Method}] {Message}", methodname, message);
			System.Diagnostics.Debug.WriteLine($"[{methodname}] INFO: {message}: {ex}");
			System.Diagnostics.Trace.WriteLine($"[{methodname}] INFO: {message}: {ex}");
		}

		public static void Warning(string message, [CallerMemberName] string methodname = "")
		{
			Logger?.LogWarning("[{Method}] {Message}", methodname, message);
			System.Diagnostics.Debug.WriteLine($"[{methodname}] WARNING: {message}");
			System.Diagnostics.Trace.WriteLine($"[{methodname}] WARNING: {message}");
		}

		public static void Warning(Exception ex, string message, [CallerMemberName] string methodname = "")
		{
			Logger?.LogWarning(ex, "[{Method}] {Message}", methodname, message);
			System.Diagnostics.Debug.WriteLine($"[{methodname}] WARNING: {message}: {ex}");
			System.Diagnostics.Trace.WriteLine($"[{methodname}] WARNING: {message}: {ex}");
		}

		public static void Error(string message, [CallerMemberName] string methodname = "")
		{
			Logger?.LogError("[{Method}] {Message}", methodname, message);
			System.Diagnostics.Debug.WriteLine($"[{methodname}] ERROR: {message}");
			System.Diagnostics.Trace.WriteLine($"[{methodname}] ERROR: {message}");
		}

		public static void Error(Exception ex, string message, [CallerMemberName] string methodname = "")
		{
			Logger?.LogError(ex, "[{Method}] {Message}", methodname, message);
			System.Diagnostics.Debug.WriteLine($"[{methodname}] ERROR: {message}: {ex}");
			System.Diagnostics.Trace.WriteLine($"[{methodname}] ERROR: {message}: {ex}");
		}
	}
}
