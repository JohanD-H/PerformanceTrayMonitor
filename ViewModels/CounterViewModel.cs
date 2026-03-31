using PerformanceTrayMonitor.Common;
using PerformanceTrayMonitor.Models;
using System;
using System.Windows;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Media;
using PerformanceTrayMonitor.Configuration;

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
			Settings = settings ?? throw new ArgumentNullException(nameof(settings));

			_useTextTrayIcon = settings.UseTextTrayIcon;
			_trayAccentColor = settings.TrayAccentColor;
			_autoTrayBackground = settings.AutoTrayBackground;
			_trayBackgroundColor = settings.TrayBackgroundColor;

			RecomputeAccentBrush();

			AttachCounter(CreateInternalCounter(settings));
		}
		public Guid Id => Settings.Id;
		public string Category => Settings.Category;
		public string Counter => Settings.Counter;
		public string Instance => Settings.Instance;
		public float Min => Settings.Min;
		public float Max => Settings.Max;
		public bool ShowInTray => Settings.ShowInTray;
		public string IconSet => Settings.IconSet;
		private bool _useTextTrayIcon;
		private Color _trayAccentColor;
		private bool _autoTrayBackground;
		private Color _trayBackgroundColor;

		//private const int MaxHistory = 60;

		private ObservableCollection<float> _history { get; } = new();
		public ObservableCollection<float> History => _history;

		//public SolidColorBrush DisplayColor { get; }
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
				_currentValue = value;
				OnPropertyChanged();

				// Update history
				History.Add(value);
				if (History.Count > TrayIconConfig.MaximumNumberOfHistoryValues)
					History.RemoveAt(0);

				// Notify SparkLine
				OnPropertyChanged(nameof(History));
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

			Settings.UseTextTrayIcon = incoming.UseTextTrayIcon;
			Settings.TrayAccentColor = incoming.TrayAccentColor;
			Settings.AutoTrayBackground = incoming.AutoTrayBackground;
			Settings.TrayBackgroundColor = incoming.TrayBackgroundColor;

			_useTextTrayIcon = incoming.UseTextTrayIcon;
			_trayAccentColor = incoming.TrayAccentColor;
			_autoTrayBackground = incoming.AutoTrayBackground;
			_trayBackgroundColor = incoming.TrayBackgroundColor;

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

		public void ForceNotifyHistory()
		{
			OnPropertyChanged(nameof(History));
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

		public bool UseTextTrayIcon
		{
			get => _useTextTrayIcon;
			set
			{
				_useTextTrayIcon = value;
				Settings.UseTextTrayIcon = value;
				OnPropertyChanged();
			}
		}

		public Color TrayAccentColor
		{
			get => _trayAccentColor;
			set
			{
				_trayAccentColor = value;
				Settings.TrayAccentColor = value;
				OnPropertyChanged();
			}
		}

		public bool AutoTrayBackground
		{
			get => _autoTrayBackground;
			set
			{
				_autoTrayBackground = value;
				Settings.AutoTrayBackground = value;
				OnPropertyChanged();
			}
		}

		public Color TrayBackgroundColor
		{
			get => _trayBackgroundColor;
			set
			{
				_trayBackgroundColor = value;
				Settings.TrayBackgroundColor = value;
				OnPropertyChanged();
			}
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
