using Microsoft.Extensions.Logging;
using PerformanceTrayMonitor.Common;
using PerformanceTrayMonitor.Configuration;
using PerformanceTrayMonitor.Models;
using PerformanceTrayMonitor.Properties;
using PerformanceTrayMonitor.Settings;
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
		private MainViewModel? _mainVm;
		public static HiddenMainWindow? HiddenWindow { get; private set; }

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
			HiddenWindow = new HiddenMainWindow();
			Application.Current.MainWindow = HiddenWindow;
			HiddenWindow.Show();
			HiddenWindow.Hide(); // stays focusable but invisible

			// -----------------------------------------
			// Load settings (with migration)
			// -----------------------------------------
			Log.Debug($"Settings loading...");
			// -----------------------------------------
			// Load settings (DTO → runtime)
			// -----------------------------------------
			var dto = SettingsRepository.Load();
			var settings = SettingsMapper.ToOptions(dto);

			Log.Debug($"Settings loaded: metrics={settings.Metrics.Count}, ShowAppIcon={settings.Global.ShowAppIcon}");

			// -----------------------------------------
			// If settings contain no metrics, use defaults
			// -----------------------------------------
			if (settings.Metrics.Count == 0)
			{
				Log.Warning("Settings file exists but contains no metrics. Using defaults.");

				var defaults = new DefaultSettingsProvider().Create();

				// Keep user’s global settings if they exist
				defaults.Global.ShowAppIcon = settings.Global.ShowAppIcon;
				defaults.Global.CustomColors = settings.Global.CustomColors;

				settings = defaults;

				// Save defaults immediately
				SettingsSaveQueue.Enqueue(SettingsMapper.ToDto(settings));
			}

			Log.Debug($"Loaded {settings.Metrics.Count} counters (ShowAppIcon={settings.Global.ShowAppIcon}, PopupWasOpen = {settings.Global.PopupWasOpen}");

			try
			{
				_mainVm = new MainViewModel(settings);

				_mainVm.Start();

				// Restore popup if pinned and previously open
				Dispatcher.InvokeAsync(() =>
				{
					if (settings.Global.PopupPinned && settings.Global.PopupWasOpen)
						_mainVm.ShowPopup();
				}, DispatcherPriority.Background);
			}
			catch (Exception ex)
			{
				Log.Error($"{ex} Failed to initialize MainViewModel. Falling back to defaults.");

				var defaults = new DefaultSettingsProvider().Create();

				SettingsSaveQueue.Enqueue(SettingsMapper.ToDto(defaults));

				_mainVm = new MainViewModel(defaults);
				_mainVm.Start();
			}

			Log.Debug($"Finished: {GetHashCode()}");
		}

		protected override void OnExit(ExitEventArgs e)
		{
			Log.Debug($"OnExit: PopupIsOpen = {_mainVm.PopupIsOpen}, PopupPinned = {_mainVm.PopupPinned}");

			if (_mainVm != null)
			{
				// 1. Restore metrics BEFORE stopping anything
				if (_mainVm.SharedConfigVm.EditorPendingEdits)
				{
					Log.Debug("OnExit: Pending edits detected — restoring last saved metrics snapshot.");
					_mainVm.SharedConfigVm.RestoreMetrics(_mainVm.SharedConfigVm.LastSavedMetricsSnapshot);
				}

				// 2. Stop background sampling, timers, etc.
				_mainVm.Stop();

				// 3. Dispose all counters to release PerformanceCounter handles
				foreach (var vm in _mainVm.Counters)
					vm.Dispose();

				// 4. Save full settings (global + restored metrics)
				var dto = SettingsMapper.ToDto(_mainVm.Settings);
				SettingsSaveQueue.Enqueue(dto);

				// 5. Force final write
				SettingsSaveQueue.Flush();
			}

			Log.Debug($"Exiting..{GetHashCode()}");

			base.OnExit(e);
		}
	}
}
