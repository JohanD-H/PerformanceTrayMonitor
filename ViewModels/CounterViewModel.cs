using PerformanceTrayMonitor.Common;
using PerformanceTrayMonitor.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;

// ----------------------------------------
// Show counter in config window
// ----------------------------------------
namespace PerformanceTrayMonitor.ViewModels
{
	public class CounterViewModel : BaseViewModel, IDisposable
	{
		private readonly CounterSettings _settings;
		private PerformanceCounter? _counter;
		private bool _disposed;

		public string DisplayNameProxy => $"{DisplayName} ({Category})";

		public CounterSettings Settings => _settings;

		public CounterViewModel(CounterSettings settings)
		{
			_settings = settings;
		}

		public string Category
		{
			get => _settings.Category;
			set
			{
				if (_settings.Category != value)
				{
					_settings.Category = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(DisplayNameProxy));
				}
			}
		}

		public string Counter
		{
			get => _settings.Counter;
			set
			{
				if (_settings.Counter != value)
				{
					_settings.Counter = value;
					OnPropertyChanged();
				}
			}
		}

		public string Instance
		{
			get => _settings.Instance;
			set
			{
				if (_settings.Instance != value)
				{
					_settings.Instance = value;
					OnPropertyChanged();
				}
			}
		}

		public string DisplayName
		{
			get => _settings.DisplayName;
			set
			{
				if (_settings.DisplayName != value)
				{
					_settings.DisplayName = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(DisplayNameProxy));
				}
			}
		}

		public float Min
		{
			get => _settings.Min;
			set
			{
				if (_settings.Min != value)
				{
					_settings.Min = value;
					OnPropertyChanged();
				}
			}
		}

		public float Max
		{
			get => _settings.Max;
			set
			{
				if (_settings.Max != value)
				{
					_settings.Max = value;
					OnPropertyChanged();
				}
			}
		}

		public bool ShowInTray
		{
			get => _settings.ShowInTray;
			set
			{
				if (_settings.ShowInTray != value)
				{
					_settings.ShowInTray = value;
					OnPropertyChanged();
				}
			}
		}

		public string IconSet
		{
			get => _settings.IconSet;
			set
			{
				if (_settings.IconSet != value)
				{
					_settings.IconSet = value;
					OnPropertyChanged();
				}
			}
		}

		public void AttachCounter(PerformanceCounter? counter)
		{
			// Dispose any previous counter before replacing
			_counter?.Dispose();
			_counter = counter;
		}

		private float _currentValue;
		public float CurrentValue
		{
			get => _currentValue;
			private set
			{
				if (_currentValue != value)
				{
					_currentValue = value;
					OnPropertyChanged();
				}
			}
		}

		private readonly List<float> _history = new();
		private const int MaxHistory = 60;

		public IReadOnlyList<float> History => _history;

		public void Update()
		{
			if (_disposed || _counter == null)
				return;

			try
			{
				CurrentValue = _counter.NextValue();

				_history.Add(CurrentValue);
				if (_history.Count > MaxHistory)
					_history.RemoveAt(0);

				OnPropertyChanged(nameof(History));
			}
			catch (Exception ex)
			{
				Log.Debug($"{ex}: Error ignored!");
				// swallow transient errors
			}
		}
		public void Dispose()
		{
			if (_disposed)
				return;

			_disposed = true;

			try
			{
				_counter?.Dispose();
			}
			catch (Exception ex)
			{
				Log.Debug($"{ex}: Error ignored!");
				// ignore disposal errors
			}

			_counter = null;
		}
	}
}
