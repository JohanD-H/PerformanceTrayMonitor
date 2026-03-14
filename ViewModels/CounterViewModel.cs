using PerformanceTrayMonitor.Common;
using PerformanceTrayMonitor.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Media;

namespace PerformanceTrayMonitor.ViewModels
{
	public class CounterViewModel : BaseViewModel, IDisposable
	{
		public CounterSettings Settings { get; private set; }
		private PerformanceCounter? _internalCounter;
		private float _currentValue;

		private SolidColorBrush _accentBrush;
		public SolidColorBrush AccentBrush
		{
			get => _accentBrush;
			private set
			{
				_accentBrush = value;
				OnPropertyChanged();
			}
		}

		public CounterViewModel(CounterSettings settings)
		{
			Log.Debug($"CounterViewModel created: {GetHashCode()}");
			Settings = settings;

			// UI & SparkLine color
			//var (brush, shadow) = UIColors.GetSoftColorFor(DisplayName);
			//DisplayColor = brush;
			//ShadowOpacity = shadow;

			RecomputeAccentBrush();

			AttachCounter(CreateInternalCounter(settings));
		}

		public string Category => Settings.Category;
		public string Counter => Settings.Counter;
		public string Instance => Settings.Instance;
		public float Min => Settings.Min;
		public float Max => Settings.Max;
		public bool ShowInTray => Settings.ShowInTray;
		public string IconSet => Settings.IconSet;
		private const int MaxHistory = 60;
		private readonly ObservableCollection<float> _history = new();
		public ObservableCollection<float> History => _history;

		public SolidColorBrush DisplayColor { get; }
		public double ShadowOpacity { get; private set; }

		private void RecomputeAccentBrush()
		{
			var (brush, shadow) = UIColors.GetSoftColorFor(DisplayName);
			AccentBrush = brush;
			ShadowOpacity = shadow;
		}

		public string DisplayName =>
			string.IsNullOrWhiteSpace(Settings.DisplayName) ? Settings.Counter : Settings.DisplayName;

		public float CurrentValue
		{
			get => _currentValue;
			set
			{
				Log.Debug($"Counter {DisplayName} updated: {value}");
				Log.Debug($"Updating {DisplayName} on VM {GetHashCode()}");

				_currentValue = value;
				OnPropertyChanged();

				// Update history
				_history.Add(value);
				if (_history.Count > MaxHistory)
					_history.RemoveAt(0);

				// Notify SparkLine
				//OnPropertyChanged(nameof(History));
			}
		}

		public void UpdateFromSettings(CounterSettings incoming)
		{
			// Update the data
			Settings.Category = incoming.Category;
			Settings.Counter = incoming.Counter;
			Settings.Instance = incoming.Instance;
			Settings.DisplayName = incoming.DisplayName;
			Settings.Min = incoming.Min;
			Settings.Max = incoming.Max;
			Settings.ShowInTray = incoming.ShowInTray;
			Settings.IconSet = incoming.IconSet;

			// Re-hook the Windows counter because the Category/Instance changed
			AttachCounter(CreateInternalCounter(Settings));

			RecomputeAccentBrush();          // <- keep color in sync with name

			// Tell WPF to refresh everything
			OnPropertyChanged(string.Empty);
		}

		public void Update()
		{
			try
			{
				if (_internalCounter != null)
				{
					CurrentValue = _internalCounter.NextValue();
				}
			}
			catch { /* Counter might have vanished/stopped */ }
		}

		public void AttachCounter(PerformanceCounter? pc)
		{
			_internalCounter?.Dispose();
			_internalCounter = pc;
			// Prime it
			try
			{
				_internalCounter?.NextValue();
			} catch { /* Nothing */ }
		}

		private PerformanceCounter? CreateInternalCounter(CounterSettings s)
		{
			try
			{
				if (string.IsNullOrEmpty(s.Instance))
					return new PerformanceCounter(s.Category, s.Counter, readOnly: true);

				return new PerformanceCounter(s.Category, s.Counter, s.Instance, readOnly: true);
			}
			catch { return null; }
		}

		public void ForceRedraw()
		{
			OnPropertyChanged(nameof(History));
		}

		public void Dispose()
		{
			_internalCounter?.Dispose();
		}
	}
}
