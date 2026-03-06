using PerformanceTrayMonitor.Common;
using PerformanceTrayMonitor.Configuration;
using PerformanceTrayMonitor.Models;
using PerformanceTrayMonitor.Tray;
using PerformanceTrayMonitor.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

// ---------------------------------------------
// Manage all tray icons, app and counters
// ---------------------------------------------
namespace PerformanceTrayMonitor.Managers
{
	public sealed class TrayIconManager : IDisposable
	{
		private readonly MainViewModel _mainVm;
		private readonly ConfigViewModel _sharedConfigVm;

		private readonly Dictionary<CounterViewModel, CounterTrayIcon> _counterIcons = new();
		private AnimatedTrayIcon _animatedIcon;

		public TrayIconManager(MainViewModel mainVm)
		{
			Log.Debug($"TrayIconManager created: {GetHashCode()}");

			_mainVm = mainVm;
			_sharedConfigVm = mainVm.SharedConfigVm;

			// Create the animated app icon
			_animatedIcon = new AnimatedTrayIcon(_sharedConfigVm, _mainVm);
			Log.Debug($"AnimatedTrayIcon created: {_animatedIcon.GetHashCode()}");

			// Create counter icons
			InitializeCounterIcons();

			// Subscribe to changes
			SubscribeToCounterEvents();

			Log.Debug($"TrayIconManager initialized: {GetHashCode()}");
		}

		// ------------------------------------------------------------
		// INITIAL CREATION
		// ------------------------------------------------------------
		private void InitializeCounterIcons()
		{
			foreach (var counter in _mainVm.Counters)
				TryCreateCounterIcon(counter);
		}

		// ------------------------------------------------------------
		// EVENT SUBSCRIPTIONS
		// ------------------------------------------------------------
		private void SubscribeToCounterEvents()
		{
			_mainVm.Counters.CollectionChanged += Counters_CollectionChanged;

			foreach (var counter in _mainVm.Counters)
				counter.Settings.PropertyChanged += CounterSettings_PropertyChanged;
		}

		private void Counters_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.NewItems != null)
			{
				foreach (CounterViewModel vm in e.NewItems)
				{
					TryCreateCounterIcon(vm);
					vm.Settings.PropertyChanged += CounterSettings_PropertyChanged;
				}
			}

			if (e.OldItems != null)
			{
				foreach (CounterViewModel vm in e.OldItems)
				{
					vm.Settings.PropertyChanged -= CounterSettings_PropertyChanged;
					RemoveCounterIcon(vm);
				}
			}
		}

		private void CounterSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (sender is not CounterSettings settings)
				return;

			var counter = _mainVm.Counters.FirstOrDefault(c => c.Settings.Id == settings.Id);
			if (counter == null)
				return;

			HandleSettingsChanged(counter, e.PropertyName);
		}

		// ------------------------------------------------------------
		// SETTINGS CHANGE HANDLING
		// ------------------------------------------------------------
		private void HandleSettingsChanged(CounterViewModel counter, string propertyName)
		{
			var settings = counter.Settings;

			if (propertyName == nameof(CounterSettings.ShowInTray))
			{
				if (settings.ShowInTray)
					TryCreateCounterIcon(counter);
				else
					RemoveCounterIcon(counter);
			}

			if (propertyName == nameof(CounterSettings.IconSet) ||
				propertyName == nameof(CounterSettings.Min) ||
				propertyName == nameof(CounterSettings.Max))
			{
				RemoveCounterIcon(counter);
				TryCreateCounterIcon(counter);
			}
		}

		// ------------------------------------------------------------
		// COUNTER ICON CREATION
		// ------------------------------------------------------------
		private void TryCreateCounterIcon(CounterViewModel counter)
		{
			var settings = counter.Settings;

			if (!settings.ShowInTray)
				return;

			// Validate icon set by name
			if (!IconSetConfig.IconSets.TryGetValue(settings.IconSet, out var set))
			{
				// Pick the first available icon set as fallback
				var fallback = IconSetConfig.IconSets.Keys.FirstOrDefault();

				if (fallback == null)
				{
					Log.Error("No icon sets available — cannot create tray icon.");
					return;
				}

				Log.Debug($"Icon set '{settings.IconSet}' not found — switching to '{fallback}'.");
				settings.IconSet = fallback;
				set = IconSetConfig.IconSets[fallback];
			}

			if (_counterIcons.Count >= TrayIconConfig.MaxCounterTrayIcons)
			{
				settings.ShowInTray = false;
				return;
			}

			if (_counterIcons.ContainsKey(counter))
				return;

			Log.Debug($"Creating CounterTrayIcon for {counter.DisplayName} using set '{settings.IconSet}'.");

			_counterIcons[counter] = new CounterTrayIcon(settings, () => counter.CurrentValue, set, _mainVm);
		}

		// ------------------------------------------------------------
		// COUNTER ICON REMOVAL
		// ------------------------------------------------------------
		private void RemoveCounterIcon(CounterViewModel counter)
		{
			if (_counterIcons.TryGetValue(counter, out var icon))
			{
				icon.Dispose();
				_counterIcons.Remove(counter);
			}
		}

		// ------------------------------------------------------------
		// FULL REBUILD (App icon + all counter icons)
		// ------------------------------------------------------------
		public void RebuildAllIcons()
		{
			Log.Debug("Rebuilding all tray icons...");

			// Dispose animated app icon
			_animatedIcon?.Dispose();
			_animatedIcon = null;

			// Dispose all counter icons
			foreach (var icon in _counterIcons.Values)
				icon.Dispose();
			_counterIcons.Clear();

			// Recreate animated app icon (only if enabled)
			if (_mainVm.ShowAppIcon)
			{
				_animatedIcon = new AnimatedTrayIcon(_sharedConfigVm, _mainVm);
				Log.Debug($"AnimatedTrayIcon recreated: {_animatedIcon.GetHashCode()}");
			}
			else
			{
				Log.Debug("ShowAppIcon = false — skipping AnimatedTrayIcon creation.");
			}

			// Recreate counter icons
			foreach (var counter in _mainVm.Counters)
				TryCreateCounterIcon(counter);

			Log.Debug("Rebuild complete.");
		}

		// ------------------------------------------------------------
		// DISPOSAL
		// ------------------------------------------------------------
		private bool _disposed;
		public void Dispose()
		{
			if (_disposed)
				return;
			
			_disposed = true;
			
			Log.Debug($"Disposing TrayIconManager: {GetHashCode()}");

			_mainVm.Counters.CollectionChanged -= Counters_CollectionChanged;

			foreach (var counter in _mainVm.Counters)
				counter.Settings.PropertyChanged -= CounterSettings_PropertyChanged;

			foreach (var icon in _counterIcons.Values)
				icon.Dispose();

			_counterIcons.Clear();

			_animatedIcon?.Dispose();
			_animatedIcon = null;
		}
	}
}
