using PerformanceTrayMonitor.Common;
using PerformanceTrayMonitor.Configuration;
using PerformanceTrayMonitor.Models;
using System;
using System.Linq;

namespace PerformanceTrayMonitor.ViewModels
{
	public class CounterEditorViewModel : BaseViewModel
	{
		private readonly ConfigViewModel _parent;
		private Guid _id;
		public Guid Id
		{
			get => _id;
			set { _id = value; OnPropertyChanged(); }
		}
		// Standard properties
		public string DisplayName { get => _displayName; set { _displayName = value; OnPropertyChanged(); } }
		public float Min { get => _min; set { _min = value; OnPropertyChanged(); } }
		public float Max { get => _max; set { _max = value; OnPropertyChanged(); } }
		public bool ShowInTray { get => _showInTray; set { _showInTray = value; OnPropertyChanged(); } }
		public string IconSet { get => _iconSet; set { _iconSet = value; OnPropertyChanged(); } }

		private string _category, _counter, _instance, _displayName;
		private float _min, _max;
		private bool _showInTray;
		private string _iconSet;

		public CounterEditorViewModel(ConfigViewModel parent) => _parent = parent;

		public string SelectedCategory
		{
			get => _category;
			set
			{
				// SHIELD: Ignore nulls pushed by UI during list rebuilds
				if (value == null && _category != null) return;
				if (_category == value) return;

				_category = value;
				OnPropertyChanged();

				// Tell parent to fill the next dropdown
				_parent.LoadCountersForCategory(value);

				// If not loading existing, pick first counter
				if (!_parent.IsLoading && _parent.CountersInCategory.Any())
					SelectedCounter = _parent.CountersInCategory.First();
			}
		}

		public string SelectedCounter
		{
			get => _counter;
			set
			{
				// SHIELD: If the parent is loading, ignore signals from the UI
				if (_parent.IsLoading)
				{
					if (_counter == value) return;
					_counter = value;
					OnPropertyChanged();
					return; // EXIT EARLY - don't trigger cascading list loads
				}

				if (value == null && _counter != null) return;
				if (_counter == value) return;

				_counter = value;
				OnPropertyChanged();
				
				// Only trigger a reload if the USER is manually changing the box
				_parent.LoadInstancesForCounter(_category, value);

				// Not my preferred method, adding delays in the UI, but I see no other way of making it work
				//
				// Use a background priority to ensure the ItemsSource binding has finished updating before we try to select the first item.
				System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
				{
					if (_parent.Instances.Any())
					{
						// We set the field directly then notify to force the ComboBox to re-evaluate the SelectedValue
						var first = _parent.Instances.First();
						_instance = first;
						OnPropertyChanged(nameof(SelectedInstance));
					}
				}), System.Windows.Threading.DispatcherPriority.Loaded); // Higher priority than 'Idle'
			}
		}

		public string SelectedInstance
		{
			get => _instance;
			set
			{
				// SHIELD: If the parent is loading, ignore signals from the UI
				// Note: This prevents the ComboBox from "nulling out" the field when the list clears.
				if (_parent.IsLoading)
				{
					var loadingClean = value?.Replace("\u00A0", " ").Trim() ?? "";
					if (_instance == loadingClean) return;
					_instance = loadingClean;
					OnPropertyChanged();
					return;
				}

				// Setter logic
				var clean = value?.Replace("\u00A0", " ").Trim() ?? "";

				// Prevent the UI from clearing a valid selection with a blank one
				if (string.IsNullOrEmpty(clean) && !string.IsNullOrEmpty(_instance)) return;
				if (_instance == clean) return;

				_instance = clean;
				OnPropertyChanged();
			}
		}

		public void LoadFrom(CounterViewModel vm)
		{
			// Map the data to the private fields
			_category = vm.Category;
			_counter = vm.Counter;
			
			// Fix for Windows Instance strings
			_instance = vm.Instance?.Replace("\u00A0", " ").Trim() ?? "";

			DisplayName = vm.DisplayName;
			Min = vm.Min;
			Max = vm.Max;
			ShowInTray = vm.ShowInTray;
			IconSet = vm.IconSet;

			// Notify basic properties
			OnPropertyChanged(nameof(DisplayName));
			OnPropertyChanged(nameof(Min));
			OnPropertyChanged(nameof(Max));
			OnPropertyChanged(nameof(ShowInTray));
			OnPropertyChanged(nameof(IconSet));
			OnPropertyChanged(nameof(SelectedCategory));

			// Notify these specifically. Because the ItemsSource (CountersInCategory) 
			// is already full, the ComboBox will see this specific 'ping', 
			// look at its list, find the match, and display it, hopefully!
			OnPropertyChanged(nameof(SelectedCounter));
			OnPropertyChanged(nameof(SelectedInstance));
		}

		// Inside CounterEditorViewModel.cs
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
			ShowInTray = true;
		}

		public CounterSettings ToSettings() => new CounterSettings
		{
			Category = _category,
			Counter = _counter,
			Instance = _instance,
			DisplayName = _displayName,
			Min = _min,
			Max = _max,
			ShowInTray = _showInTray,
			IconSet = _iconSet
		};
	}
}
