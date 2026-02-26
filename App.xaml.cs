using PerformanceTrayMonitor.Configuration;
//using PerformanceTrayMonitor.Managers;
using PerformanceTrayMonitor.Models;
//using PerformanceTrayMonitor.Tray;
using PerformanceTrayMonitor.ViewModels;
using Serilog;
using Serilog.Enrichers.WithCaller;
using System;
//using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using static PerformanceTrayMonitor.Configuration.AppIdentity;
using static PerformanceTrayMonitor.Configuration.Config;

namespace PerformanceTrayMonitor
{
	public partial class App : Application
	{
		private MainViewModel _mainVm;

		protected override void OnStartup(StartupEventArgs e)
		{

#if DEBUG
			{
				// 1. Get the timestamp
				string TimeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

				// 2. Get the folder where the .exe is running
				string BaseDir = AppDomain.CurrentDomain.BaseDirectory;

				// 3. Go up 3 levels to reach the Project Root (PerfLED/)
				// Level 1: net8.0-windows, Level 2: Debug, Level 3: bin
				DirectoryInfo ProjectRoot = Directory.GetParent(BaseDir).Parent.Parent.Parent;

				// 4. Create the Logs folder in the root
				string LogFolder = Path.Combine(ProjectRoot.FullName, "Logs");
				if (!Directory.Exists(LogFolder))
					Directory.CreateDirectory(LogFolder);

				// 5. Define the log path dynamically
				string LogPath = Path.Combine(LogFolder, $"{AppId}_{TimeStamp}.txt");
				//string LogPath = $"./Logs/{ProjectName}_{TimeStamp}.txt";

				// 6. Setup the debug logger
				Log.Logger = new LoggerConfiguration()
					.MinimumLevel.Debug()
					.Enrich.WithCaller() // No prefix needed!
					.WriteTo.File(LogPath,
						// Use {Caller} to get "Namespace.Class.Method"
						outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] ({Caller:40}) {Message:lj}{NewLine}{Exception}")
					.CreateLogger();
				/*
				Log.Logger = new LoggerConfiguration()
					.MinimumLevel.Debug()
					.Enrich.FromLogContext()
					.WriteTo.File(LogPath,
						outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] ({SourceContext}) {Message:lj}{NewLine}{Exception}")
					.CreateLogger();
				*/
			}
#else
			{
				// In Release, Serilog will simply "swallow" all logs silently
				Log.Logger = Serilog.Core.Logger.None; 
			}
#endif
			Log.Debug($"Startup: {GetHashCode()}");

			base.OnStartup(e);

			// ----------------------------------------- 
			// 1. Load settings (with migration) 
			// -----------------------------------------
			var settings = SettingsStore.Load();

			// -----------------------------------------
			// 2. If settings are empty, use defaults 
			// -----------------------------------------
			if (settings.Count == 0)
			{
				Log.Warning("Settings file exists but contains no counters. Using defaults.");
				var defaults = new DefaultSettingsProvider().Create();
				settings.AddRange(defaults.Counters);

				SettingsStore.Save(settings);
			}
			Log.Debug("Settings count passed to MainViewModel = " + settings.Count());

			// ----------------------------------------- 
			// 3. Initialize MainViewModel with settings 
			// -----------------------------------------
			_mainVm = new MainViewModel(settings);

			// NEW: Create the main tray icon
			//_appTrayIcon = new AppTrayIcon(_mainVm);

			// NEW: Create the manager that handles all counter tray icons
			//_trayIconManager = new TrayIconManager(_mainVm);

			// ----------------------------------------- 
			// 4. Start tray icons, UI, etc. 
			// -----------------------------------------
			_mainVm.Start();
			Log.Debug($"Finished: {GetHashCode()}");
		}

		protected override void OnExit(ExitEventArgs e)
		{
			_mainVm?.Stop();
			SettingsStore.Save(_mainVm.GetSettingsSnapshot());

#if DEBUG
			Log.Debug($"Exiting..{GetHashCode()}");

			// Always flush and close before the app exits
			Log.CloseAndFlush();
#endif

			base.OnExit(e);
		}
	}
}
