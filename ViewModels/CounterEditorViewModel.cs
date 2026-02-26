using PerformanceTrayMonitor.Configuration;
using PerformanceTrayMonitor.Models;
using System;

namespace PerformanceTrayMonitor.ViewModels
{
	public class CounterEditorViewModel : BaseViewModel
	{
		private Guid _id;
		private string _category = "";
		private string _counter = "";
		private string _instance = "";
		private string _displayName = "";
		private float _min;
		private float _max;
		private string _mode = "Activity";
		private bool _showInTray;
		private string _iconSet = "Activity";

		public Guid Id
		{
			get => _id;
			set { _id = value; OnPropertyChanged(); }
		}

		public string Category
		{
			get => _category;
			set { _category = value; OnPropertyChanged(); }
		}

		public string Counter
		{
			get => _counter;
			set { _counter = value; OnPropertyChanged(); }
		}

		public string Instance
		{
			get => _instance;
			set { _instance = value; OnPropertyChanged(); }
		}

		public string DisplayName
		{
			get => _displayName;
			set { _displayName = value; OnPropertyChanged(); }
		}

		public float Min
		{
			get => _min;
			set { _min = value; OnPropertyChanged(); }
		}

		public float Max
		{
			get => _max;
			set { _max = value; OnPropertyChanged(); }
		}

		public string Mode
		{
			get => _mode;
			set { _mode = value; OnPropertyChanged(); }
		}

		public bool ShowInTray
		{
			get => _showInTray;
			set { _showInTray = value; OnPropertyChanged(); }
		}

		public string IconSet
		{
			get => _iconSet;
			set { _iconSet = value; OnPropertyChanged(); }
		}

		public void LoadFrom(CounterViewModel source)
		{
			Id = source.Settings.Id;
			Category = source.Category;
			Counter = source.Counter;
			Instance = source.Instance;
			DisplayName = source.DisplayName;
			Min = source.Min;
			Max = source.Max;
			Mode = source.Mode;
			ShowInTray = source.ShowInTray;
			IconSet = source.IconSet;
		}

		public CounterSettings ToSettings()
		{
			return new CounterSettings
			{
				Id = Id,
				Category = Category,
				Counter = Counter,
				Instance = Instance,
				DisplayName = DisplayName,
				Min = Min,
				Max = Max,
				Mode = Mode,
				ShowInTray = ShowInTray,
				IconSet = IconSet
			};
		}

		public void LoadDefaults()
		{
			Id = Guid.NewGuid();
			Category = CounterConfig.DefaultCategory;
			Counter = CounterConfig.DefaultCounter;
			Instance = CounterConfig.DefaultInstance;
			DisplayName = CounterConfig.DefaultDisplayName;
			Min = CounterConfig.DefaultMin;
			Max = CounterConfig.DefaultMax;
			Mode = CounterConfig.DefaultMode;
			IconSet = CounterConfig.DefaultIconSet;
		}
	}
}
