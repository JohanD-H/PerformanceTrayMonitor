using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Windows.Media; // for Color

// ----------------------------------
// Changing counter values
// ----------------------------------
namespace PerformanceTrayMonitor.Models
{
	public class CounterSettings : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler? PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string name = null)
	=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

		private bool SetField<T>(ref T field, T value, [CallerMemberName] string name = null)
		{
			if (EqualityComparer<T>.Default.Equals(field, value))
				return false;

			field = value;
			OnPropertyChanged(name);
			return true;
		}

		public string FullCounterPath
		{
			get
			{
				// Example: Category = "Processor", Instance = "_Total", Counter = "% Processor Time"

				if (!string.IsNullOrWhiteSpace(Instance))
					return $@"\\{Category}({Instance})\{Counter}";

				return $@"\\{Category}\{Counter}";
			}
		}

		// Stable identity so TrayIconManager can match settings safely
		public Guid Id { get; set; } = Guid.NewGuid();

		private string _category = "";
		public string Category
		{
			get => _category;
			set => SetField(ref _category, value ?? "");
		}

		private string _counter = "";
		public string Counter
		{
			get => _counter;
			set => SetField(ref _counter, value ?? "");
		}

		private string _instance = "";
		public string Instance
		{
			get => _instance;
			set => SetField(ref _instance, value ?? "");
		}

		private string _displayName = "";
		public string DisplayName
		{
			get => _displayName;
			set => SetField(ref _displayName, value ?? "");
		}

		private float _min;
		public float Min
		{
			get => _min;
			set => SetField(ref _min, value);
		}

		private float _max;
		public float Max
		{
			get => _max;
			set => SetField(ref _max, value);
		}

		private bool _showInTray;
		public bool ShowInTray
		{
			get => _showInTray;
			set => SetField(ref _showInTray, value);
		}

		private string _iconSet = "";
		public string IconSet
		{
			get => _iconSet;
			set => SetField(ref _iconSet, value ?? "");
		}

		// -------------------------------
		// Tray text icon settings
		// -------------------------------

		private bool _useTextTrayIcon;
		public bool UseTextTrayIcon
		{
			get => _useTextTrayIcon;
			set => SetField(ref _useTextTrayIcon, value);
		}

		private Color _trayAccentColor = Colors.White;
		public Color TrayAccentColor
		{
			get => _trayAccentColor;
			set => SetField(ref _trayAccentColor, value);
		}

		private bool _autoTrayBackground = true;
		public bool AutoTrayBackground
		{
			get => _autoTrayBackground;
			set => SetField(ref _autoTrayBackground, value);
		}

		private Color _trayBackgroundColor = Colors.Black;
		public Color TrayBackgroundColor
		{
			get => _trayBackgroundColor;
			set => SetField(ref _trayBackgroundColor, value);
		}
	}
}
