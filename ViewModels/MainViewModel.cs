using PerformanceTrayMonitor.Common;
using PerformanceTrayMonitor.Models;
using PerformanceTrayMonitor.Settings;
using PerformanceTrayMonitor.Tray;
using PerformanceTrayMonitor.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Threading;

namespace PerformanceTrayMonitor.ViewModels
{
	public class MainViewModel : BaseViewModel
	{
		public ObservableCollection<CounterViewModel> Counters { get; } = new();

		private readonly DispatcherTimer _timer;

		private PopupWindow? _popup;

		private TrayIconManager _trayIconManager;

		private ConfigWindow? _configWindow;

		// The full settings object (global + metrics)
		public SettingsOptions Settings { get; private set; }

		// Shared ConfigViewModel
		public ConfigViewModel SharedConfigVm { get; }

		// Used to suppress undesired App Icon changes
		internal bool _suppressReevaluation;

		internal bool _suppressAppIconReeval;

		// Popup pinning
		private bool _popupPinned;
		public bool PopupIsOpen => _popup != null && _popup.IsLoaded;

		// ------------------------------------------------------------
		// CONSTRUCTOR
		// ------------------------------------------------------------
		public MainViewModel(SettingsOptions settings)
		{
			Settings = settings ?? throw new ArgumentNullException(nameof(settings));
			_popupPinned = Settings.Global.PopupPinned;

			_timer = new DispatcherTimer
			{
				Interval = TimeSpan.FromSeconds(1)
			};
			_timer.Tick += (s, e) => Tick();

			LoadCounters(Settings.Metrics);

			// SharedConfigVm now receives the full settings snapshot
			SharedConfigVm = new ConfigViewModel(GetSettingsSnapshot(), this);

			_trayIconManager = new TrayIconManager(this);

			SharedConfigVm.OnMetricPendingRemoval = metric =>
			{
				if (metric.ShowInTray)
				{
					// Update tray icon count (derived property will reflect it)
					OnPropertyChanged(nameof(SharedConfigVm.TrayIconCount));
					OnPropertyChanged(nameof(SharedConfigVm.TrayIconCountDisplay));

					// Rebuild tray icons to reflect the intended state
					_trayIconManager.RebuildAllIcons();
				}
			};

			SharedConfigVm.OnMetricAdded = metric =>
			{
				if (metric.ShowInTray)
					_trayIconManager.RebuildAllIcons();

				OnPropertyChanged(nameof(SharedConfigVm.TrayIconCount));
				OnPropertyChanged(nameof(SharedConfigVm.TrayIconCountDisplay));
			};

			SharedConfigVm.OnMetricCopied = metric =>
			{
				if (metric.ShowInTray)
					_trayIconManager.RebuildAllIcons();

				OnPropertyChanged(nameof(SharedConfigVm.TrayIconCount));
				OnPropertyChanged(nameof(SharedConfigVm.TrayIconCountDisplay));
			};

			SharedConfigVm.OnMetricUpdated = metric =>
			{
				if (metric.ShowInTray)
					_trayIconManager.RebuildAllIcons();

				OnPropertyChanged(nameof(SharedConfigVm.TrayIconCount));
				OnPropertyChanged(nameof(SharedConfigVm.TrayIconCountDisplay));
			};


			foreach (var vm in Counters)
				vm.AttachCounter(CreateCounter(vm.Settings));
		}

		// ------------------------------------------------------------
		// COUNTER LOADING
		// ------------------------------------------------------------
		private void LoadCounters(IEnumerable<CounterSettings> metrics)
		{
			Counters.Clear();

			if (metrics == null)
				return;

			foreach (var settings in metrics)
				Counters.Add(new CounterViewModel(settings));
		}

		// ------------------------------------------------------------
		// GLOBAL SETTING: ShowAppIcon
		// ------------------------------------------------------------
		public bool ShowAppIcon
		{
			get => Settings.Global.ShowAppIcon;
			set
			{
				//Log.Debug($"ShowAppIcon: Settings.Global.ShowAppIcon = {Settings.Global.ShowAppIcon}, value = {value}");
				if (Settings.Global.ShowAppIcon != value)
				{
					Settings.Global.ShowAppIcon = value;
					if (!_suppressReevaluation)
						OnPropertyChanged();
				}
			}
		}

		// ------------------------------------------------------------
		// TICK
		// ------------------------------------------------------------
		private void Tick()
		{
			foreach (var c in Counters)
				c.Update();
		}

		public void Start()
		{
			// Prime counters
			foreach (var c in Counters)
			{
				// No this is not a typo, we purposely do two updates (create + initialize)
				c.Update(); // Create historical counters
				c.Update(); // Set historical counters to 0
							// Now the historical counters are fully initialized!
			}

			_timer.Start();
		}

		public void Stop() => _timer.Stop();

		// ------------------------------------------------------------
		// SETTINGS SNAPSHOT (for saving / config)
		// ------------------------------------------------------------
		public SettingsOptions GetSettingsSnapshot()
		{
			// Settings is already the runtime model; no need to rebuild
			return Settings;
		}

		// ------------------------------------------------------------
		// REPLACE SETTINGS (used after config window save)
		// ------------------------------------------------------------
		public void ReplaceSettings(SettingsOptions newSettings)
		{
			if (newSettings == null)
			{
				Log.Error("ReplaceSettings called with null SettingsOptions.");
				return;
			}

			// Dispose tray icons first (they may still reference counters)
			_trayIconManager?.Dispose();

			// Dispose old counters
			foreach (var vm in Counters)
				vm.Dispose();

			// Replace full settings object
			Settings = newSettings;
			_popupPinned = Settings.Global.PopupPinned;

			// Reload counters (creates new CounterViewModels)
			LoadCounters(Settings.Metrics);

			// Rebuild tray icons
			_trayIconManager = new TrayIconManager(this);

			foreach (var vm in Counters)
			{
				vm.AttachCounter(CreateCounter(vm.Settings));

				// PRIME COUNTERS (Do this twice!!)
				vm.Update(); // prime
				vm.Update(); // real value
			}

			// DELAY UI REDRAW UNTIL LAYOUT IS READY
			UiAfterLayout.Run(() =>
			{
				foreach (var vm in Counters)
					vm.ForceRedraw();
			});
		}

		// ------------------------------------------------------------
		// POPUP WINDOW
		// ------------------------------------------------------------
		public void ShowPopup()
		{
			if (_popup != null && _popup.IsLoaded)
			{
				_popup.Activate();
				return;
			}

			foreach (var c in Counters)
				c.Update(); // Update historical counter value

			_popup = new PopupWindow(this)
			{
				WindowStartupLocation = Settings.Global.PopupPinned
					? WindowStartupLocation.Manual
					: WindowStartupLocation.CenterScreen,

				Owner = Settings.Global.PopupPinned ? null : System.Windows.Application.Current.MainWindow
			};

			bool canRestore = false;  // Restore gate is closed

			// Pre-position before showing
			if (Settings.Global.PopupPinned)
			{
				// Try to find the saved monitor
				var index = Settings.Global.PopupMonitorId ?? -1;

				Screen screen = null;
				if (index >= 0 && index < Screen.AllScreens.Length)
					screen = Screen.AllScreens[index];

				if (screen != null)
				{
					canRestore = true; // Open restore gate

					// Convert saved DPI → current DPI
					double savedDpi = Settings.Global.PopupDpi ?? 96;
					double currentDpi = 96;

					var source = PresentationSource.FromVisual(System.Windows.Application.Current.MainWindow);
					if (source?.CompositionTarget != null)
						currentDpi = 96.0 * source.CompositionTarget.TransformToDevice.M11;

					double scale = currentDpi / savedDpi;

					double x = Settings.Global.PopupX ?? 0;
					double y = Settings.Global.PopupY ?? 0;

					_popup.Left = x * scale;
					_popup.Top = y * scale;
				}
				else
				{
					// Remove pinned setting
					Settings.Global.PopupPinned = false;
					// Center instead of restoring
					_popup.WindowStartupLocation = WindowStartupLocation.CenterScreen;
				}
			}

			_popup.Closed += (s, e) => _popup = null;

			_popup.Show();
			_popup.Opacity = 1;
			// Give the popup keyboard focus so InputBindings work
			_popup.Activate();
			_popup.Focus();
			Keyboard.Focus(_popup);

			// Always delay — layout needs to settle
			_popup.Loaded += (s, e) =>
			{
				UiAfterLayout.Run(_popup, () =>
				{
					if (canRestore)
						RestorePopupPosition(_popup);
				});
			};

			Settings.Global.PopupWasOpen = true;
		}

		public void ClosePopup()
		{
			if (_popup != null)
			{
				_popup.Close();
				_popup = null;
				Settings.Global.PopupWasOpen = false;
			}
		}

		public void TogglePopup()
		{
			if (PopupIsOpen)
			{
				if (!PopupPinned)
					ClosePopup();
				else
					_popup.Activate();
			}
			else
			{
				ShowPopup();
			}
		}

		public void ShowAppIconExplicit()
		{
			//Log.Debug($"ShowAppIconExplicit: ShowAppIcon = {ShowAppIcon}");
			if (!ShowAppIcon)
			{
				_suppressAppIconReeval = true;
				ShowAppIcon = true;
				_suppressAppIconReeval = false;
				_trayIconManager.RebuildAllIcons();
				SettingsSaveQueue.Enqueue(SettingsMapper.ToDto(Settings));
			}
		}

		// ------------------------------------------------------------
		// CONFIG WINDOW
		// ------------------------------------------------------------
		public void ShowConfig()
		{
			if (_configWindow != null)
			{
				_configWindow.Activate();
				return;
			}

			var freshVm = new ConfigViewModel(GetSettingsSnapshot(), this);

			_configWindow = new ConfigWindow(freshVm)
			{
				WindowStartupLocation = WindowStartupLocation.CenterScreen
			};

			_configWindow.Closed += (s, e) => _configWindow = null;
			_configWindow.Show();
		}

		public void ToggleAppIcon()
		{
			//Log.Debug($"ToggleAppIcon: Before ShowAppIcon = {ShowAppIcon}");

			// Temporarily suppress reevaluation
			_suppressReevaluation = true;

			ShowAppIcon = !ShowAppIcon;

			_suppressReevaluation = false;

			// Now rebuild deterministically
			_trayIconManager.RebuildAllIcons();

			SettingsSaveQueue.Enqueue(SettingsMapper.ToDto(Settings));
		}

		public void ShowGraph(CounterViewModel vm)
		{
			var window = new MetricGraphWindow(vm);
			window.Show();
		}

		// ------------------------------------------------------------
		// PERFORMANCE COUNTER CREATION
		// ------------------------------------------------------------
		private static PerformanceCounter? CreateCounter(CounterSettings settings)
		{
			try
			{
				if (string.IsNullOrEmpty(settings.Instance))
					return new PerformanceCounter(settings.Category, settings.Counter, readOnly: true);

				return new PerformanceCounter(settings.Category, settings.Counter, settings.Instance, readOnly: true);
			}
			catch
			{
				return null;
			}
		}

		public bool PopupPinned
		{
			get => _popupPinned;
			set
			{
				if (_popupPinned != value)
				{
					_popupPinned = value;
					Settings.Global.PopupPinned = value;
					OnPropertyChanged();

					if (_popup != null)
					{
						if (_popupPinned)
						{
							_popup.Topmost = false;
							_popup.Topmost = true;
							_popup.Activate();

							SavePopupPosition();
						}
						else
						{
							_popup.Topmost = false;
							Settings.Global.PopupWasOpen = _popup?.IsVisible ?? false;
						}
					}
				}
			}
		}

		private void SavePopupPosition()
		{
			if (_popup == null)
				return;

			var screen = System.Windows.Forms.Screen.FromHandle(
				new System.Windows.Interop.WindowInteropHelper(_popup).Handle);

			Settings.Global.PopupPinned = true;
			Settings.Global.PopupMonitorId = Array.IndexOf(Screen.AllScreens, screen);

			// Save raw device pixels
			Settings.Global.PopupX = _popup.Left;
			Settings.Global.PopupY = _popup.Top;

			// Save DPI
			var source = PresentationSource.FromVisual(_popup);
			if (source?.CompositionTarget != null)
			{
				Settings.Global.PopupDpi = 96.0 * source.CompositionTarget.TransformToDevice.M11;
			}
		}

		private void RestorePopupPosition(Window popup)
		{
			if (!Settings.Global.PopupPinned)
				return;

			// Find the monitor
			var screens = System.Windows.Forms.Screen.AllScreens;
			var index = Settings.Global.PopupMonitorId ?? -1;

			Screen screen = null;
			if (index >= 0 && index < screens.Length)
				screen = screens[index];

			screen ??= Screen.PrimaryScreen;

			// DPI scaling
			double savedDpi = Settings.Global.PopupDpi ?? 96.0;
			double currentDpi = 96.0;

			var source = PresentationSource.FromVisual(popup);
			if (source?.CompositionTarget != null)
				currentDpi = 96.0 * source.CompositionTarget.TransformToDevice.M11;

			double scale = currentDpi / savedDpi;

			double x = (Settings.Global.PopupX ?? 0) * scale;
			double y = (Settings.Global.PopupY ?? 0) * scale;

			// Clamp inside working area
			var wa = screen.WorkingArea;
			x = Math.Max(wa.Left, Math.Min(x, wa.Right - popup.Width));
			y = Math.Max(wa.Top, Math.Min(y, wa.Bottom - popup.Height));

			popup.WindowStartupLocation = WindowStartupLocation.Manual;
			popup.Left = x;
			popup.Top = y;
		}
	}
}
