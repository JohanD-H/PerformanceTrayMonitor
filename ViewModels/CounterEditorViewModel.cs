using PerformanceTrayMonitor.Configuration;
using PerformanceTrayMonitor.Models;
using System;
using System.Linq;

// ------------------------------------------
// Editing a counter
// ------------------------------------------
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
		private bool _showInTray;
		private string _iconSet = "Activity";
		private bool _suppressReactiveUpdates;

		private readonly ConfigViewModel _config;

		public CounterEditorViewModel(ConfigViewModel config)
		{
			_config = config;
		}

		public string SelectedCategory
		{
			get => Category;
			set
			{
				if (Category != value)
				{
					Category = value;
					OnPropertyChanged(nameof(SelectedCategory));

					if (_suppressReactiveUpdates)
						return;

					_config.LoadCountersForCategory(value);

					if (_config.CountersInCategory.Any() &&
						!_config.CountersInCategory.Contains(Counter))
					{
						Counter = _config.CountersInCategory.First();
						OnPropertyChanged(nameof(SelectedCounter));
					}

					_config.LoadInstancesForCounter(Category, Counter);

					if (_config.Instances.Any() &&
						!_config.Instances.Contains(Instance))
					{
						Instance = _config.Instances.First();
						OnPropertyChanged(nameof(SelectedInstance));
					}
				}
			}
		}

		public string SelectedCounter
		{
			get => Counter;
			set
			{
				if (Counter != value)
				{
					Counter = value;
					OnPropertyChanged(nameof(SelectedCounter));

					if (_suppressReactiveUpdates)
						return;

					_config.LoadInstancesForCounter(Category, value);

					if (_config.Instances.Any() &&
						!_config.Instances.Contains(Instance))
					{
						Instance = _config.Instances.First();
						OnPropertyChanged(nameof(SelectedInstance));
					}
				}
			}
		}

		public string SelectedInstance
		{
			get => Instance;
			set
			{
				if (Instance != value)
				{
					Instance = value;
					OnPropertyChanged(nameof(SelectedInstance));

					if (_suppressReactiveUpdates)
						return;

					// Validate instance
					if (_config.Instances.Any() &&
						!_config.Instances.Contains(Instance))
					{
						Instance = _config.Instances.First();
						OnPropertyChanged(nameof(SelectedInstance));
					}
				}
			}
		}

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
			_suppressReactiveUpdates = true;

			Id = source.Settings.Id;
			SelectedCategory = source.Category;
			SelectedCounter = source.Counter;
			SelectedInstance = source.Instance;
			DisplayName = source.DisplayName;
			Min = source.Min;
			Max = source.Max;
			ShowInTray = source.ShowInTray;
			IconSet = source.IconSet;

			_suppressReactiveUpdates = false;
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
				ShowInTray = ShowInTray,
				IconSet = IconSet
			};
		}

		public void LoadDefaults()
		{
			var defaults = new DefaultSettingsProvider().CreateDefaultCounter();

			Id = Guid.NewGuid();
			SelectedCategory = defaults.Category;
			SelectedCounter = defaults.Counter;
			SelectedInstance = defaults.Instance;
			DisplayName = defaults.DisplayName;
			Min = defaults.Min;
			Max = defaults.Max;
			IconSet = defaults.IconSet;
		}
	}
}
