using PerformanceTrayMonitor.Common;
using PerformanceTrayMonitor.Models;
using System;
using System.Diagnostics;

namespace PerformanceTrayMonitor.ViewModels
{
	public class CounterViewModel : BaseViewModel, IDisposable
	{
		public CounterSettings Settings { get; private set; }
		private PerformanceCounter? _internalCounter;
		private float _currentValue;

		public CounterViewModel(CounterSettings settings)
		{
			Settings = settings;
			AttachCounter(CreateInternalCounter(settings));
		}

		public string Category => Settings.Category;
		public string Counter => Settings.Counter;
		public string Instance => Settings.Instance;
		public string DisplayName => string.IsNullOrWhiteSpace(Settings.DisplayName) ? Settings.Counter : Settings.DisplayName;
		public float Min => Settings.Min;
		public float Max => Settings.Max;
		public bool ShowInTray => Settings.ShowInTray;
		public string IconSet => Settings.IconSet;

		public float CurrentValue
		{
			get => _currentValue;
			set { _currentValue = value; OnPropertyChanged(); }
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
			try { _internalCounter?.NextValue(); } catch { }
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

		public void Dispose()
		{
			_internalCounter?.Dispose();
		}
	}
}
