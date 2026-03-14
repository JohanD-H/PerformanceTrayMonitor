using Microsoft.Extensions.Logging;
using PerformanceTrayMonitor.Common;
using PerformanceTrayMonitor.Configuration;
using PerformanceTrayMonitor.Models;
using PerformanceTrayMonitor.Properties;
using PerformanceTrayMonitor.ViewModels;
using PerformanceTrayMonitor.Views;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace PerformanceTrayMonitor
{
	public partial class App : Application
	{
		private MainViewModel _mainVm;

		protected override void OnStartup(StartupEventArgs e)
		{
			var loggerFactory = LoggerFactory.Create(builder =>
			{
#if DEBUG
				builder.SetMinimumLevel(LogLevel.Debug);
				// VS Output window
				builder.AddDebug();
				// DebugView
				builder.AddTraceSource(
					new SourceSwitch("sourceSwitch", "Verbose"),
					new DefaultTraceListener()
				);
#else
				builder.SetMinimumLevel(LogLevel.None);
#endif
			});
			Log.Logger = loggerFactory.CreateLogger("App");

			Log.Debug($"Startup: {GetHashCode()}");

			base.OnStartup(e);

			// Create the hidden main window
			var hidden = new HiddenMainWindow();
			
			// Set it as the real MainWindow
			Application.Current.MainWindow = hidden;

			// Show the hidden main window
			hidden.Show();

			// -----------------------------------------
			// Load settings (with migration)
			// -----------------------------------------
			Log.Debug($"Settings loading...");
			SettingsOptions settings = SettingsStore.Load();
			Log.Debug($"Settings loaded..., count = {settings.Counters.Count}");

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
				// Restore PopupWindow, if it was OPEN and PINNED on close!
				Dispatcher.InvokeAsync(() =>
				{
					if (settings.PopupPinned && settings.PopupWasOpen)
						_mainVm.ShowPopup();
				}, DispatcherPriority.Background);
			}
			catch (Exception ex)
			{
				Log.Error($"{ex} Failed to initialize MainViewModel. Falling back to defaults.");

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

				// Save the PopupWindow state!
				//Log.Debug($"OnExit: PopupIsOpen = {_mainVm.PopupIsOpen}");
				//_mainVm.Settings.PopupWasOpen = _mainVm.PopupIsOpen;
				// Save full settings (global + counters)
				SettingsStore.Save(_mainVm.GetSettingsSnapshot());
			}

			Log.Debug($"Exiting..{GetHashCode()}");

			base.OnExit(e);
		}
	}
}
