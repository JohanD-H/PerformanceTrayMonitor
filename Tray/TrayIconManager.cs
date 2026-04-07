using PerformanceTrayMonitor.Common;
using PerformanceTrayMonitor.Configuration;
using PerformanceTrayMonitor.Models;
using PerformanceTrayMonitor.ViewModels;
using System;
using System.Windows;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

// ---------------------------------------------
// Manage all tray icons, app and counters
// ---------------------------------------------
namespace PerformanceTrayMonitor.Tray
{
	public sealed class TrayIconManager : IDisposable
	{
		private readonly MainViewModel _mainVm;

		private readonly ConfigViewModel _sharedConfigVm;

		private readonly Dictionary<CounterViewModel, CounterTrayIcon> _counterIcons = new();

		private AnimatedTrayIcon? _animatedIcon;

		public TrayIconManager(MainViewModel mainVm)
		{
			_mainVm = mainVm;
			_sharedConfigVm = mainVm.SharedConfigVm;

			// Create counter icons first
			InitializeCounterIcons();

			// Create the app icon according to the golden rule
			InitializeAppIcon();

			// Subscribe to changes, NO LONGER USEFULL!
			//SubscribeToCounterEvents();
			_mainVm.PropertyChanged += MainVm_PropertyChanged;

			foreach (var icon in _counterIcons.Values)
				icon.UpdateContextMenu();
		}

		// ------------------------------------------------------------
		// INITIAL CREATION
		// ------------------------------------------------------------
		private void InitializeCounterIcons()
		{
			foreach (var counter in _mainVm.Counters)
				TryCreateCounterIcon(counter);
		}

		private void InitializeAppIcon()
		{
			//Log.Debug("TrayIconManager: creating AnimatedTrayIcon");

			bool hasCounters = _counterIcons.Count > 0;

			// ---> Rule:  vvvvvvvvvvvvvvvvv
			// - If no counters → always create app icon
			// - If counters exist → create only if ShowAppIcon = true
			if (!hasCounters || _mainVm.ShowAppIcon)
			{
				_animatedIcon = new AnimatedTrayIcon(_sharedConfigVm, _mainVm);
			}
			else
			{
				// Nothing
			}
		}

		// ------------------------------------------------------------
		// EVENT SUBSCRIPTIONS
		// ------------------------------------------------------------
		private void CounterSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (sender is not CounterSettings settings)
				return;
			Log.Debug($"CounterSettings_PropertyChanged: Id = {settings.Id}");

			var counter = _mainVm.Counters.FirstOrDefault(c => c.Settings.Id == settings.Id);
			if (counter == null)
				return;

			HandleSettingsChanged(counter, e.PropertyName);
		}

		private void MainVm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (_mainVm._suppressReevaluation)
				return;

			if (e.PropertyName == nameof(MainViewModel.ShowAppIcon))
			{
				//ReevaluateAppIcon();

				// Also update counter menus
				foreach (var icon in _counterIcons.Values)
					icon.UpdateContextMenu();
			}
		}

		/*
		private void SubscribeToCounterEvents()
		{
			_mainVm.Counters.CollectionChanged += Counters_CollectionChanged;

			foreach (var counter in _mainVm.Counters)
				counter.Settings.PropertyChanged += CounterSettings_PropertyChanged;
		}
		*/

		private void Counters_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			Log.Debug($"Counters_CollectionChanged: NewItems = {e.NewItems}, OldItems = {e.OldItems}");
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

			// After counters change, re-evaluate the rule
			//ReevaluateAppIcon();
		}

		/*
		private void ReevaluateAppIcon()
		{
			Log.Debug("TrayIconManager: Reevaluating AnimatedTrayIcon");

			bool hasCounters = _counterIcons.Count > 0;
			bool shouldHaveAppIcon = !hasCounters || _mainVm.ShowAppIcon;

			if (shouldHaveAppIcon)
			{
				// Create only if missing
				if (_animatedIcon == null)
				{
					Log.Debug("TrayIconManager: creating AnimatedTrayIcon");
					_animatedIcon = new AnimatedTrayIcon(_sharedConfigVm, _mainVm);
				}
			}
			else
			{
				// Dispose only if present
				if (_animatedIcon != null)
				{
					Log.Debug("TrayIconManager: disposing AnimatedTrayIcon");
					_animatedIcon.Dispose();
					_animatedIcon = null;
				}
			}

			foreach (var icon in _counterIcons.Values)
				icon.UpdateContextMenu();
		}

		private void ReevaluateAppIcon()
		{
			bool hasCounters = _counterIcons.Count > 0;

			Log.Debug("TrayIconManager: Reevaluating AnimatedTrayIcon");
			// If no counters → ensure app icon exists
			if (!hasCounters)
			{
				if (_animatedIcon == null)
				{
					_animatedIcon = new AnimatedTrayIcon(_sharedConfigVm, _mainVm);
				}
				return;
			}

			// If counters exist → app icon depends on ShowAppIcon
			if (_mainVm.ShowAppIcon)
			{
				if (_animatedIcon == null)
				{
					_animatedIcon = new AnimatedTrayIcon(_sharedConfigVm, _mainVm);
				}
			}
			else
			{
				_animatedIcon?.Dispose();
				_animatedIcon = null;
			}

			// NEW: update counter menus
			foreach (var icon in _counterIcons.Values)
				icon.UpdateContextMenu();
		}
		*/

		// ------------------------------------------------------------
		// FULL REBUILD
		// ------------------------------------------------------------
		public void RebuildAllIcons()
		{
			//Log.Debug("RebuildAllIcons: disposing app + counters");

			_animatedIcon?.Dispose();
			_animatedIcon = null;

			foreach (var icon in _counterIcons.Values)
				icon.Dispose();
			_counterIcons.Clear();

			InitializeCounterIcons();
			InitializeAppIcon();

			// Update counter menus
			foreach (var icon in _counterIcons.Values)
				icon.UpdateContextMenu();
		}

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

				settings.IconSet = fallback;
				set = IconSetConfig.IconSets[fallback];
			}

			if (_counterIcons.Count >= TrayIconConfig.MaxCounterTrayIcons)
			{
				Log.Debug($"TryCreateCounterIcon: _counterIcon.Count = {_counterIcons.Count}, setting ShowInTray false!");
				settings.ShowInTray = false;
				return;
			}

			if (_counterIcons.ContainsKey(counter))
				return;

			_counterIcons[counter] = new CounterTrayIcon(settings, () => counter.CurrentValue, set, _mainVm);
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
		// DISPOSAL
		// ------------------------------------------------------------
		private bool _disposed;
		public void Dispose()
		{
			if (_disposed)
				return;

			_disposed = true;

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
