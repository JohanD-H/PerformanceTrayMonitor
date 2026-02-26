using PerformanceTrayMonitor.Configuration;
using PerformanceTrayMonitor.Managers;
using PerformanceTrayMonitor.Models;
using PerformanceTrayMonitor.Views;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Threading;

namespace PerformanceTrayMonitor.ViewModels
{
	public class MainViewModel : BaseViewModel
	{
		public ObservableCollection<CounterViewModel> Counters { get; } = new();

		private readonly DispatcherTimer _timer;
		private PopupWindow _popup;
		public bool IsPopupOpen => _popup != null && _popup.IsLoaded;
		private TrayIconManager _trayIconManager;
		private ConfigWindow _configWindow;

		// The single shared ConfigViewModel for the entire app
		public ConfigViewModel ConfigVm { get; }

		public bool PopupIsOpen => _popup != null && _popup.IsLoaded;

		public MainViewModel(IEnumerable<CounterSettingsDto> dtos)
		{
			Log.Debug($"MainViewModel created: {GetHashCode()}");

			_timer = new DispatcherTimer
			{
				Interval = TimeSpan.FromSeconds(1)
			};
			_timer.Tick += (s, e) => Tick();

			// Load counters first
			LoadCounters(dtos);

			// Create the single shared ConfigViewModel
			ConfigVm = new ConfigViewModel(GetSettingsSnapshot(), this);

			// Pass *this* (MainViewModel) to TrayIconManager
			_trayIconManager = new TrayIconManager(this);

			// Attach performance counters
			foreach (var vm in Counters)
				vm.AttachCounter(CreateCounter(vm.Settings));

			Log.Debug($"MainViewModel initialized: {GetHashCode()}");
		}

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
					Mode = dto.Mode,
					ShowInTray = dto.ShowInTray,
					IconSet = dto.IconSet
				};

				Counters.Add(new CounterViewModel(settings));
			}
		}

		private void Tick()
		{
			foreach (var c in Counters)
				c.Update();
		}

		public void Start() => _timer.Start();
		public void Stop() => _timer.Stop();

		public List<CounterSettingsDto> GetSettingsSnapshot()
		{
			return Counters
				.Select(c => new CounterSettingsDto
				{
					Id = c.Settings.Id,
					Category = c.Category,
					Counter = c.Counter,
					Instance = c.Instance,
					DisplayName = c.DisplayName,
					Min = c.Min,
					Max = c.Max,
					Mode = c.Mode,
					ShowInTray = c.ShowInTray,
					IconSet = c.IconSet
				})
				.ToList();
		}

		public void ReplaceCounters(IEnumerable<CounterSettingsDto> dtos)
		{
			Log.Debug($"ReplaceCounters called: {GetHashCode()}");

			_trayIconManager?.Dispose();

			LoadCounters(dtos);

			// Recreate tray icon manager with the same MainViewModel
			_trayIconManager = new TrayIconManager(this);

			foreach (var vm in Counters)
				vm.AttachCounter(CreateCounter(vm.Settings));
		}

		public void ShowPopup()
		{
			if (_popup != null && _popup.IsLoaded)
			{
				_popup.Activate();
				return;
			}

			_popup = new PopupWindow
			{
				DataContext = this
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

		public void ShowConfig()
		{
			if (_configWindow != null)
			{
				_configWindow.Activate();
				return;
			}

			// Always reuse the single shared ConfigVm
			_configWindow = new ConfigWindow(ConfigVm);
			_configWindow.Closed += (s, e) => _configWindow = null;

			_configWindow.Show();
		}

		public void TogglePopup()
		{
			if (PopupIsOpen)
				ClosePopup();
			else
				ShowPopup();
		}

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
	}
}
