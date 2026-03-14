using PerformanceTrayMonitor.Common;
using PerformanceTrayMonitor.Configuration;
using PerformanceTrayMonitor.Managers;
using PerformanceTrayMonitor.Models;
using PerformanceTrayMonitor.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
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

		// The full settings object (global + counters)
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

			Settings = settings;
			_popupPinned = settings.PopupPinned;

			_timer = new DispatcherTimer
			{
				Interval = TimeSpan.FromSeconds(1)
			};
			_timer.Tick += (s, e) => Tick();

			LoadCounters(settings.Counters);

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
		private void LoadCounters(IEnumerable<CounterSettingsDto> dtos)
		{
			Counters.Clear();

			foreach (var dto in dtos)
			{
				var settings = new CounterSettings
				{
					Id = dto.Id,
					Category = dto.Category,
					Counter = dto.Counter,
					Instance = dto.Instance,
					DisplayName = dto.DisplayName,
					Min = dto.Min,
					Max = dto.Max,
					ShowInTray = dto.ShowInTray,
					IconSet = dto.IconSet
				};

				Counters.Add(new CounterViewModel(settings));
			}
		}

		// ------------------------------------------------------------
		// GLOBAL SETTING: ShowAppIcon
		// ------------------------------------------------------------
		public bool ShowAppIcon
		{
			get => Settings.ShowAppIcon;
			set
			{
				if (Settings.ShowAppIcon != value)
				{
					Settings.ShowAppIcon = value;
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
				c.Update();
				c.Update();
			}

			_timer.Start();
		}

		public void Stop() => _timer.Stop();

		// ------------------------------------------------------------
		// SETTINGS SNAPSHOT (for saving)
		// ------------------------------------------------------------
		public SettingsOptions GetSettingsSnapshot()
		{
			Log.Debug($"GetSettingsSnapshot: ShowAppIcon = {this.ShowAppIcon}");
			Log.Debug($"GetSettingsSnapshot: PopupPinned = {this.Settings.PopupPinned}");
			Log.Debug($"GetSettingsSnapshot: PopupMonitorId = {this.Settings.PopupMonitorId}");
			Log.Debug($"GetSettingsSnapshot: PopupX = {this.Settings.PopupX}");
			Log.Debug($"GetSettingsSnapshot: PopupY = {this.Settings.PopupY}");
			Log.Debug($"GetSettingsSnapshot: PopupDpi = {this.Settings.PopupDpi}");
			Log.Debug($"GetSettingsSnapshot: PopupWasOpen = {this.Settings.PopupWasOpen}");

			return new SettingsOptions(
				Counters.Select(c => new CounterSettingsDto
				{
					Id = c.Settings.Id,
					Category = c.Category,
					Counter = c.Counter,
					Instance = c.Instance,
					DisplayName = c.DisplayName,
					Min = c.Min,
					Max = c.Max,
					ShowInTray = c.ShowInTray,
					IconSet = c.IconSet
				}).ToList(),
				this.ShowAppIcon,
				SettingsOptions.CurrentVersion
			)
			{
				PopupPinned = this.Settings.PopupPinned,
				PopupMonitorId = this.Settings.PopupMonitorId,
				PopupX = this.Settings.PopupX,
				PopupY = this.Settings.PopupY,
				PopupDpi = this.Settings.PopupDpi,
				PopupWasOpen = this.Settings.PopupWasOpen
			};
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

			// Reload counters (creates new CounterViewModels)
			LoadCounters(newSettings.Counters);

			// Rebuild tray icons
			_trayIconManager = new TrayIconManager(this);

			foreach (var vm in Counters)
			{ 
				vm.AttachCounter(CreateCounter(vm.Settings));

				// ⭐ PRIME COUNTERS (same fix as startup)
				vm.Update(); // prime
				vm.Update(); // real value
			}

			// ⭐ DELAY UI REDRAW UNTIL LAYOUT IS READY
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

			Log.Debug("MainWindow = " + System.Windows.Application.Current.MainWindow?.GetType().Name);

			foreach (var c in Counters)
				c.Update();

			_popup = new PopupWindow
			{
				WindowStartupLocation = Settings.PopupPinned
					? WindowStartupLocation.Manual
					: WindowStartupLocation.CenterScreen,

				DataContext = this,
				Owner = Settings.PopupPinned ? null : System.Windows.Application.Current.MainWindow
			};

			bool canRestore = false;  // Restore gate is closed

			// Pre-position before showing
			if (Settings.PopupPinned)
			{
				// Try to find the saved monitor
				var screen = Screen.AllScreens.FirstOrDefault(s => s.DeviceName == Settings.PopupMonitorId);

				if (screen != null)
				{
					canRestore = true; // Open restore gate

					// Convert saved DPI → current DPI
					double savedDpi = Settings.PopupDpi ?? 96;
					double currentDpi = 96;

					var source = PresentationSource.FromVisual(System.Windows.Application.Current.MainWindow);
					if (source?.CompositionTarget != null)
						currentDpi = 96.0 * source.CompositionTarget.TransformToDevice.M11;

					double scale = currentDpi / savedDpi;

					double x = Settings.PopupX ?? 0;
					double y = Settings.PopupY ?? 0;

					_popup.Left = x * scale;
					_popup.Top = y * scale;
				}
				else
				{
					// Saved monitor missing
					// Remove pinned setting
					Settings.PopupPinned = false;
					// Center instead of restoring
					_popup.WindowStartupLocation = WindowStartupLocation.CenterScreen;
				}
			}

			_popup.Closed += (s, e) => _popup = null;

			_popup.Show();

			// Always delay — layout needs to settle
			_popup.Loaded += (s, e) =>
			{
				UiAfterLayout.Run(_popup, () =>
				{
					if (canRestore)
						RestorePopupPosition(_popup);
				});
			};

			Settings.PopupWasOpen = true;
		}

		public void ClosePopup()
		{
			if (_popup != null)
			{
				_popup.Close();
				_popup = null;
				Settings.PopupWasOpen = false;
				Log.Debug($"PopupWasOpen = {Settings.PopupWasOpen}");
			}
		}

		public void TogglePopup()
		{
			if (PopupIsOpen)
			{
				Log.Debug($"TogglePopup: PopupPinned = {PopupPinned}");
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

			_configWindow.Closed += (s, e) => _configWindow = null;
			_configWindow.ShowDialog();
		}

		public void ToggleAppIcon()
		{
			ShowAppIcon = !ShowAppIcon;

			// Rebuild tray icons
			_trayIconManager.RebuildAllIcons();

			// Save settings
			//SettingsStore.Save(Settings);
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
					Log.Debug($"PopupPinned: _popupPinned = {_popupPinned}, PopupWasOpen = {Settings.PopupWasOpen}");
					Settings.PopupPinned = value;
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
							Settings.PopupWasOpen = false;
						}
					}

					Log.Debug($"PopupPinned: PopupPinned = {Settings.PopupPinned}");
					//SettingsStore.Save(Settings);   // <-- CRITICAL
				}
			}
		}

		private void SavePopupPosition()
		{
			if (_popup == null)
				return;

			var screen = System.Windows.Forms.Screen.FromHandle(
				new System.Windows.Interop.WindowInteropHelper(_popup).Handle);

			Settings.PopupPinned = true;
			Settings.PopupMonitorId = screen.DeviceName;

			// Save raw device pixels
			Settings.PopupX = _popup.Left;
			Settings.PopupY = _popup.Top;

			// Save DPI
			var source = PresentationSource.FromVisual(_popup);
			if (source?.CompositionTarget != null)
			{
				Settings.PopupDpi = 96.0 * source.CompositionTarget.TransformToDevice.M11;
			}

			//SettingsStore.Save(Settings);
		}

		private void RestorePopupPosition(Window popup)
		{
			Log.Debug($"RestorePopupPosition: PopupPinned = {Settings.PopupPinned}");
			if (!Settings.PopupPinned)
				return;

			// Find the monitor
			var screens = System.Windows.Forms.Screen.AllScreens;
			var screen = screens.FirstOrDefault(s => s.DeviceName == Settings.PopupMonitorId)
					  ?? System.Windows.Forms.Screen.PrimaryScreen;

			// DPI scaling
			double savedDpi = Settings.PopupDpi ?? 96.0;
			double currentDpi = 96.0;

			var source = PresentationSource.FromVisual(popup);
			if (source?.CompositionTarget != null)
				currentDpi = 96.0 * source.CompositionTarget.TransformToDevice.M11;

			double scale = currentDpi / savedDpi;

			double x = (Settings.PopupX ?? 0) * scale;
			double y = (Settings.PopupY ?? 0) * scale;

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