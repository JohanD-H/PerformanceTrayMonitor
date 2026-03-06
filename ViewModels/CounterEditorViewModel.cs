using PerformanceTrayMonitor.Common;
using PerformanceTrayMonitor.Configuration;
using PerformanceTrayMonitor.Models;
using System;
using System.Linq;

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

		// -----------------------------
		// CATEGORY
		// -----------------------------
		public string SelectedCategory
		{
			get => Category;
			set
			{
				if (_suppressReactiveUpdates)
				{
					Log.Debug($"[SC] Suppressed SET Category='{value}'");
					return;
				}

				Log.Debug($"[SC] SET Category='{value}', Old='{Category}'");

				if (Category != value)
				{
					_suppressReactiveUpdates = true;

					Category = value;
					OnPropertyChanged(nameof(SelectedCategory));

					SelectedCounter = null;
					SelectedInstance = null;

					Log.Debug("[SC] Loading counters...");
					_config.LoadCountersForCategory(value);
					Log.Debug($"[SC] Counters loaded: {string.Join(", ", _config.CountersInCategory)}");

					EnsureValidCounterSelection();

					Log.Debug("[SC] Loading instances...");
					_config.LoadInstancesForCounter(Category, Counter);
					Log.Debug($"[SC] Instances loaded: {string.Join(", ", _config.Instances)}");

					EnsureValidInstanceSelection();

					_suppressReactiveUpdates = false;
				}
			}
		}

		// -----------------------------
		// COUNTER
		// -----------------------------
		public string SelectedCounter
		{
			get => Counter;
			set
			{
				if (_suppressReactiveUpdates)
				{
					Log.Debug("[CT] Suppressed");
					return;
				}

				Log.Debug($"[CT] SET Counter='{value}', Old='{Counter}'");

				if (string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(Counter))
				{
					Log.Debug("[CT] Ignored empty write from WPF");
					return;
				}

				if (Counter != value)
				{
					Counter = value;
					OnPropertyChanged(nameof(SelectedCounter));

					Log.Debug("[CT] Loading instances...");
					_config.LoadInstancesForCounter(Category, value);
					Log.Debug($"[CT] Instances loaded: {string.Join(", ", _config.Instances)}");

					EnsureValidInstanceSelection();
				}
			}
		}

		// -----------------------------
		// INSTANCE
		// -----------------------------
		public string SelectedInstance
		{
			get => Instance;
			set
			{
				if (_suppressReactiveUpdates)
				{
					Log.Debug("[IN] Suppressed");
					return;
				}

				Log.Debug($"[IN] SET Instance='{value}', Old='{Instance}'");

				// Ignore WPF's transient empty writes
				if (string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(Instance))
				{
					Log.Debug("[IN] Ignored empty write from WPF");
					return;
				}

				if (Instance != value)
				{
					Instance = value;
					OnPropertyChanged(nameof(SelectedInstance));

					// Ensure the instance is valid for the current category/counter
					EnsureValidInstanceSelection();
				}
			}
		}

		// -----------------------------

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
			set
			{
				if (_min != value)
				{
					_min = value;

					if (_max < _min)
					{
						Log.Debug($"[RANGE] Adjusting Max from {_max} to {_min} because Min was set to {_min}.");
						_max = _min;
						OnPropertyChanged(nameof(Max));
					}

					OnPropertyChanged();
				}
			}
		}

		public float Max
		{
			get => _max;
			set
			{
				if (_max != value)
				{
					_max = value;

					if (_min > _max)
					{
						Log.Debug($"[RANGE] Adjusting Min from {_min} to {_max} because Max was set to {_max}.");
						_min = _max;
						OnPropertyChanged(nameof(Min));
					}

					OnPropertyChanged();
				}
			}
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

		// -----------------------------
		// LOAD FROM EXISTING COUNTER
		// -----------------------------
		public void LoadFrom(CounterViewModel source)
		{
			Log.Debug($"[LF] START LoadFrom: Cat='{source.Category}', Ctr='{source.Counter}', Inst='{source.Instance}'");

			_suppressReactiveUpdates = true;

			Id = source.Settings.Id;
			DisplayName = source.DisplayName;
			Min = source.Min;
			Max = source.Max;
			ShowInTray = source.ShowInTray;
			IconSet = source.IconSet;

			// Set raw values without triggering auto-selection
			_category = source.Category;
			_counter = source.Counter;
			_instance = source.Instance;

			_suppressReactiveUpdates = false;

			// Ensure lists are loaded
			_config.LoadCountersForCategory(Category);
			_config.LoadInstancesForCounter(Category, Counter);

			// Now run through the normal selection logic
			SelectedCategory = Category;
			SelectedCounter = Counter;
			SelectedInstance = Instance;

			Log.Debug("[LF] UI notified");
		}

		// -----------------------------
		// SAVE TO SETTINGS
		// -----------------------------
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

		// -----------------------------
		// LOAD DEFAULTS
		// -----------------------------
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

		private void EnsureValidCounterSelection()
		{
			if (_config.CountersInCategory.Any())
			{
				if (!_config.CountersInCategory.Contains(Counter))
				{
					var first = _config.CountersInCategory.First();
					Log.Debug($"[SC] Auto-selecting counter: '{first}'");
					SelectedCounter = first;
				}
			}
		}

		private void EnsureValidInstanceSelection()
		{
			if (_config.Instances.Any())
			{
				if (!_config.Instances.Contains(Instance))
				{
					var first = _config.Instances.First();
					Log.Debug($"Auto-selecting instance: '{first}'");
					SelectedInstance = first;
				}
			}
		}
	}
}
