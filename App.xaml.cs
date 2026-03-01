using PerformanceTrayMonitor.Configuration;
using PerformanceTrayMonitor.Models;
using PerformanceTrayMonitor.ViewModels;
using Serilog;
using Serilog.Enrichers.WithCaller;
using System;
using System.IO;
using System.Windows;
using static PerformanceTrayMonitor.Configuration.AppIdentity;

namespace PerformanceTrayMonitor
{
	public partial class App : Application
	{
		private MainViewModel _mainVm;

		protected override void OnStartup(StartupEventArgs e)
		{
#if DEBUG
			{
				string TimeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
				string BaseDir = AppDomain.CurrentDomain.BaseDirectory;

				DirectoryInfo ProjectRoot = Directory.GetParent(BaseDir).Parent.Parent.Parent;

				string LogFolder = Path.Combine(ProjectRoot.FullName, "Logs");
				if (!Directory.Exists(LogFolder))
					Directory.CreateDirectory(LogFolder);

				string LogPath = Path.Combine(LogFolder, $"{AppId}_{TimeStamp}.txt");

				Log.Logger = new LoggerConfiguration()
					.MinimumLevel.Debug()
					.Enrich.WithCaller()
					.WriteTo.File(LogPath,
						outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] ({Caller:40}) {Message:lj}{NewLine}{Exception}")
					.CreateLogger();
			}
#else
            {
                Log.Logger = Serilog.Core.Logger.None;
            }
#endif

			Log.Debug($"Startup: {GetHashCode()}");

			base.OnStartup(e);

			// -----------------------------------------
			// Load settings (with migration)
			// -----------------------------------------
			SettingsOptions settings = SettingsStore.Load();

			// -----------------------------------------
			// If settings contain no counters, use defaults
			// -----------------------------------------
			if (settings.Counters.Count == 0)
			{
				Log.Warning("Settings file exists but contains no counters. Using defaults.");

				var defaults = new DefaultSettingsProvider().Create();

				// Keep user’s global settings if they exist, otherwise use defaults
				settings = new SettingsOptions(
					defaults.Counters,
					settings.ShowAppIcon
				);

				SettingsStore.Save(settings);
			}

			Log.Debug($"Loaded {settings.Counters.Count} counters (ShowAppIcon={settings.ShowAppIcon}, Version={settings.Version})");

			try
			{
				// -----------------------------------------
				// Initialize MainViewModel with full settings
				// -----------------------------------------
				_mainVm = new MainViewModel(settings);

				// -----------------------------------------
				// Start tray icons, UI, etc.
				// -----------------------------------------
				_mainVm.Start();
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Failed to initialize MainViewModel. Falling back to defaults.");

				var defaults = new DefaultSettingsProvider().Create();
				SettingsStore.Save(defaults);

				_mainVm = new MainViewModel(defaults);
				_mainVm.Start();
			}

			Log.Debug($"Finished: {GetHashCode()}");
		}

		protected override void OnExit(ExitEventArgs e)
		{
			_mainVm?.Stop();

			if (_mainVm != null)
			{
				// Dispose all counters to release PerformanceCounter handles
				foreach (var vm in _mainVm.Counters)
					vm.Dispose();

				// Save full settings (global + counters)
				SettingsStore.Save(_mainVm.GetSettingsSnapshot());
			}

#if DEBUG
			Log.Debug($"Exiting..{GetHashCode()}");
			Log.CloseAndFlush();
#endif

			base.OnExit(e);
		}
	}
}
