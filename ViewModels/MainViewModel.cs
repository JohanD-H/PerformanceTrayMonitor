using PerformanceTrayMonitor.Common;
using PerformanceTrayMonitor.Configuration;
using PerformanceTrayMonitor.Managers;
using PerformanceTrayMonitor.Models;
using PerformanceTrayMonitor.Settings;
using PerformanceTrayMonitor.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
//using System.Windows.Media;
using System.Windows.Threading;

namespace PerformanceTrayMonitor.ViewModels
{
	public class MainViewModel : BaseViewModel
	{
		public ObservableCollection<CounterViewModel> Counters { get; } = new();

		private readonly DispatcherTimer _timer;
		private PopupWindow _popup;
		private TrayIconManager _trayIconManager;
		private ConfigWindow _configWindow;

		// The full settings object (global + metrics)
		public SettingsOptions Settings { get; private set; }

		// Shared ConfigViewModel
		public ConfigViewModel SharedConfigVm { get; }

		// Popup pinning
		private bool _popupPinned;
		public bool PopupIsOpen => _popup != null && _popup.IsLoaded;

		// ------------------------------------------------------------
		// CONSTRUCTOR
		// ------------------------------------------------------------
		public MainViewModel(SettingsOptions settings)
		{
			Log.Debug($"MainViewModel created: {GetHashCode()}");

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

			foreach (var vm in Counters)
				vm.AttachCounter(CreateCounter(vm.Settings));

			Log.Debug($"MainViewModel initialized: {GetHashCode()}");
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
				if (Settings.Global.ShowAppIcon != value)
				{
					Settings.Global.ShowAppIcon = value;
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
				// No this is not typo, we purposely do two updates 
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

			Log.Debug($"ReplaceSettings called: {GetHashCode()}");

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

				// PRIME COUNTERS (same fix as startup)
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

			Log.Debug($"ShowPopup: MainWindow = {System.Windows.Application.Current.MainWindow?.GetType().Name}, PopupIsOpen = {PopupIsOpen}");

			foreach (var c in Counters)
				c.Update(); // Update historical counter value

			_popup = new PopupWindow
			{
				WindowStartupLocation = Settings.Global.PopupPinned
					? WindowStartupLocation.Manual
					: WindowStartupLocation.CenterScreen,

				DataContext = this,
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
					// Saved monitor missing
					// Remove pinned setting
					Settings.Global.PopupPinned = false;
					// Center instead of restoring
					_popup.WindowStartupLocation = WindowStartupLocation.CenterScreen;
				}
			}

			_popup.Closed += (s, e) => _popup = null;

			_popup.Show();
			// ⭐ Give the popup keyboard focus so InputBindings work
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
			Log.Debug($"ShowPopup: PopupWasOpen = {Settings.Global.PopupWasOpen}, PopupIsOpen = {PopupIsOpen}");
		}

		public void ClosePopup()
		{
			if (_popup != null)
			{
				_popup.Close();
				_popup = null;
				Settings.Global.PopupWasOpen = false;
				Log.Debug($"PopupWasOpen = {Settings.Global.PopupWasOpen}, PopupIsOpen = {PopupIsOpen}");
			}
		}

		public void TogglePopup()
		{
			if (PopupIsOpen)
			{
				Log.Debug($"TogglePopup: PopupPinned = {PopupPinned}, PopupIsOpen = {PopupIsOpen}");
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
			if (!ShowAppIcon)
			{
				ShowAppIcon = true;
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

			Log.Debug("MainWindow = " + System.Windows.Application.Current.MainWindow?.GetType().Name);

			var freshVm = new ConfigViewModel(GetSettingsSnapshot(), this);

			_configWindow = new ConfigWindow(freshVm)
			{
				WindowStartupLocation = WindowStartupLocation.CenterScreen
			};

			Log.Debug($"ShowConfig: _configWindow hash = {_configWindow.GetHashCode()}");

			_configWindow.Closed += (s, e) => _configWindow = null;
			_configWindow.Show();
		}

		public void ToggleAppIcon()
		{
			ShowAppIcon = !ShowAppIcon;

			// Rebuild tray icons
			_trayIconManager.RebuildAllIcons();

			// Persist the change
			SettingsSaveQueue.Enqueue(SettingsMapper.ToDto(Settings));
		}

		// ------------------------------------------------------------
		// PERFORMANCE COUNTER CREATION
		// ------------------------------------------------------------
		private PerformanceCounter? CreateCounter(CounterSettings settings)
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
					Log.Debug($"PopupPinned: _popupPinned = {_popupPinned}, PopupWasOpen = {Settings.Global.PopupWasOpen}");
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
			Log.Debug($"RestorePopupPosition: PopupPinned = {Settings.Global.PopupPinned}");
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
			Log.Debug($"RestorePopupPosition: Left = {x}, Top = {y}");
		}
	}
}
