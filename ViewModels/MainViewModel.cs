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
		//public bool PopupPinned { get; set; }
		private bool _popupPinned;
		public bool PopupIsOpen => _popup != null && _popup.IsLoaded;

		// ------------------------------------------------------------
		// CONSTRUCTOR
		// ------------------------------------------------------------
		public MainViewModel(SettingsOptions settings)
		{
			Log.Debug($"MainViewModel created: {GetHashCode()}");

			Settings = settings;

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

		public void Start() => _timer.Start();
		public void Stop() => _timer.Stop();

		// ------------------------------------------------------------
		// SETTINGS SNAPSHOT (for saving)
		// ------------------------------------------------------------
		public SettingsOptions GetSettingsSnapshot()
		{
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
				this.ShowAppIcon
			);
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

			// Reload counters
			LoadCounters(newSettings.Counters);

			// Rebuild tray icons
			_trayIconManager = new TrayIconManager(this);

			foreach (var vm in Counters)
				vm.AttachCounter(CreateCounter(vm.Settings));
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

			Log.Debug("MainWindow = " + Application.Current.MainWindow?.GetType().Name);
			_popup = new PopupWindow
			{
				WindowStartupLocation = WindowStartupLocation.CenterScreen,
				DataContext = this,
				Owner = Application.Current.MainWindow   // <-- now valid and correct
			};

			_popup.Closed += (s, e) => _popup = null;
			_popup.Show();
		}

		public void ClosePopup()
		{
			if (_popup != null)
			{
				_popup.Close();
				_popup = null;
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

			Log.Debug("MainWindow = " + Application.Current.MainWindow?.GetType().Name);

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
			SettingsStore.Save(Settings);
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
					OnPropertyChanged();

					if (_popup != null)
					{
						if (_popupPinned)
						{
							// Reassert global topmost
							_popup.Topmost = false;
							_popup.Topmost = true;
							_popup.Activate();
						}
						else
						{
							_popup.Topmost = false;
						}
					}
				}
			}
		}

	}
}