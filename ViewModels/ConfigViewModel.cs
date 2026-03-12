using PerformanceTrayMonitor.Common;
using PerformanceTrayMonitor.Configuration;
using PerformanceTrayMonitor.Models;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;

namespace PerformanceTrayMonitor.ViewModels
{
	public class ConfigViewModel : BaseViewModel
	{
		private readonly MainViewModel _main;
		private CounterViewModel? _selected;
		private bool _isLoading;			// UI update gate
		private bool _hasPendingEdits;

		public event Action? RequestClose;
		public Func<bool>? ConfirmReset { get; set; }

		// 1. Rename the private field and public property
		private SettingsOptions _globalSettings;
		public SettingsOptions GlobalSettings
		{
			get => _globalSettings;
			set { _globalSettings = value; OnPropertyChanged(); }
		}

		// Collections
		public ObservableCollection<CounterViewModel> Counters { get; } = new();
		public ObservableCollection<string> Categories { get; } = new();
		public ObservableCollection<string> CountersInCategory { get; } = new();
		public ObservableCollection<string> Instances { get; } = new();
		public ObservableCollection<string> AvailableIconSets { get; } = new(IconSetConfig.IconSets.Keys.OrderBy(x => x));

		public CounterEditorViewModel Editor { get; }

		// Commands
		public ICommand AddCommand { get; private set; }
		public ICommand ApplyCommand { get; private set; }
		public ICommand CancelCommand { get; private set; }
		public ICommand RemoveCommand { get; private set; }
		public ICommand ResetCommand { get; private set; }
		public ICommand SaveCommand { get; private set; }

		public bool IsLoading => _isLoading;


		public ConfigViewModel(SettingsOptions settings, MainViewModel main)
		{
			// Now there is NO ambiguity. 
			// settings (parameter) goes into GlobalSettings (property).
			this.GlobalSettings = settings;

			_main = main;
			Editor = new CounterEditorViewModel(this);

			// 1. Initial Load of Categories (Do this once)
			var cats = PerformanceCounterCategory.GetCategories().Select(c => c.CategoryName).OrderBy(x => x);
			foreach (var cat in cats) Categories.Add(cat);

			// 2. Load Existing Counters from Settings
			foreach (var dto in settings.Counters)
			{
				Counters.Add(new CounterViewModel(new CounterSettings
				{
					Id = dto.Id,
					Category = dto.Category,
					Counter = dto.Counter,
					Instance = dto.Instance,
					DisplayName = dto.DisplayName,
					Min = dto.Min,
					Max = dto.Max,
					ShowInTray = dto.ShowInTray,
					IconSet = dto.IconSet
				}));
			}

			InitializeCommands();

			// 3. Set Initial Selection
			if (Counters.Any())
				Selected = Counters.First();
			else 
				Editor.LoadDefaults();

			// Track changes in the editor
			Editor.PropertyChanged += (s, e) => { if (!_isLoading) HasPendingEdits = true; };

		}

		public CounterViewModel? Selected
		{
			get => _selected;
			set
			{
				if (_selected == value) return;
				_selected = value;
				OnPropertyChanged();

				if (_selected != null)
				{
					_isLoading = true; // Lock the gate

					// 1. Fill the collections immediately
					LoadCountersForCategory(_selected.Category);
					LoadInstancesForCounter(_selected.Category, _selected.Counter);

					// 2. DELAY the selection slightly
					// This waits for the ComboBoxes to finish 'seeing' the new list items
					// before we try to set the SelectedItem/SelectedCounter.
					//System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
					//{
						Editor.LoadFrom(_selected);
						_isLoading = false; // Open the gate
						HasPendingEdits = false;
					//}), System.Windows.Threading.DispatcherPriority.ContextIdle);
				}
				RefreshCommandStates();
			}
		}

		private SettingsOptions _settings;
		public SettingsOptions Settings
		{
			get => _settings;
			set
			{
				_settings = value;
				OnPropertyChanged();
			}
		}

		public void LoadCountersForCategory(string category)
		{
			CountersInCategory.Clear();
			if (string.IsNullOrEmpty(category)) return;
			try
			{
				var cat = new PerformanceCounterCategory(category);
				var names = cat.GetInstanceNames().Length == 0
					? cat.GetCounters().Select(c => c.CounterName)
					: cat.GetInstanceNames().SelectMany(i => cat.GetCounters(i)).Select(c => c.CounterName);

				foreach (var n in names.Distinct().OrderBy(x => x)) CountersInCategory.Add(n);
			}
			catch { }
		}

		public void LoadInstancesForCounter(string category, string counter)
		{
			Instances.Clear();
			if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(counter)) return;
			try
			{
				var cat = new PerformanceCounterCategory(category);
				var insts = cat.GetInstanceNames();
				if (insts.Length == 0) { Instances.Add(""); return; }

				foreach (var i in insts.OrderBy(x => x))
					if (cat.GetCounters(i).Any(c => c.CounterName == counter)) Instances.Add(i);
			}
			catch { }
		}


		private void InitializeCommands()
		{
			AddCommand = new RelayCommand(_ => AddNewCounter());
			ApplyCommand = new RelayCommand(_ => ApplyEditorToSelected(), _ => Selected != null && HasPendingEdits);
			CancelCommand = new RelayCommand(_ => CancelEdits(), _ => Selected != null && HasPendingEdits);
			RemoveCommand = new RelayCommand(_ => RemoveSelected(), _ => Selected != null);
			ResetCommand = new RelayCommand(_ => { if (ConfirmReset?.Invoke() ?? true) ResetToDefaults(); });
			SaveCommand = new RelayCommand(_ => Save());
		}

		private void AddNewCounter()
		{
			// 1. Capture current UI state
			var settings = Editor.ToSettings();
			settings.Id = Guid.NewGuid();

			// 2. Create the VM
			var vm = new CounterViewModel(settings);

			// 3. Add it to the collection
			Counters.Add(vm);

			// 4. PRE-LOAD the lists BEFORE changing 'Selected'
			// This ensures the ComboBoxes have items BEFORE they try to bind to the new VM
			_isLoading = true;	// Close the gate

			LoadCountersForCategory(vm.Category);
			LoadInstancesForCounter(vm.Category, vm.Counter);

			// 5. Now update the selection
			Selected = vm;

			_isLoading = false;		// Open the gate
			HasPendingEdits = false;
			RefreshCommandStates();
		}

		private void ApplyEditorToSelected()
		{
			if (Selected == null) return;
			var settings = Editor.ToSettings();
			Selected.UpdateFromSettings(settings); // Assume this method updates the VM properties
			HasPendingEdits = false;
			RefreshCommandStates();
		}

		private void CancelEdits()
		{
			if (Selected != null) Editor.LoadFrom(Selected);
			HasPendingEdits = false;
			RefreshCommandStates();
		}

		private void RemoveSelected()
		{
			if (Selected == null) return;
			int idx = Counters.IndexOf(Selected);
			Counters.Remove(Selected);
			if (Counters.Any()) Selected = Counters[Math.Min(idx, Counters.Count - 1)];
			else Editor.LoadDefaults();
		}

		private void Save()
		{
			// Apply any final changes from the editor to the selected list item
			if (Selected != null && HasPendingEdits)
				ApplyEditorToSelected();

			// Create the final settings object to save to disk
			var finalSettings = new SettingsOptions(
				Counters.Select(c => new CounterSettingsDto
				{
					Id = c.Settings.Id,
					Category = c.Category,
					Counter = c.Counter,
					Instance = c.Instance,
					DisplayName = c.DisplayName,
					Min = c.Min,
					Max = c.Max,
					ShowInTray = c.ShowInTray,
					IconSet = c.IconSet
				}).ToList(),
				_main.ShowAppIcon
			)
			{
				PopupPinned = this.GlobalSettings.PopupPinned,
				PopupMonitorId = this.GlobalSettings.PopupMonitorId,
				PopupX = this.GlobalSettings.PopupX,
				PopupY = this.GlobalSettings.PopupY,
				PopupDpi = this.GlobalSettings.PopupDpi,
				PopupWasOpen = this.GlobalSettings.PopupWasOpen
			};

			// Push to the main loop and save to file
			_main.ReplaceSettings(finalSettings);
			SettingsStore.Save(finalSettings);

			RequestClose?.Invoke();
		}

		public bool HasPendingEdits
		{
			get => _hasPendingEdits;
			private set { _hasPendingEdits = value; OnPropertyChanged(); RefreshCommandStates(); }
		}

		private void RefreshCommandStates()
		{
			(ApplyCommand as RelayCommand)?.RaiseCanExecuteChanged();
			(CancelCommand as RelayCommand)?.RaiseCanExecuteChanged();
			(RemoveCommand as RelayCommand)?.RaiseCanExecuteChanged();
		}

		private void ResetToDefaults()
		{
			Counters.Clear();
			var def = new DefaultSettingsProvider().CreateDefaultCounter();
			Counters.Add(new CounterViewModel(new CounterSettings
			{
				Category = def.Category,
				Counter = def.Counter,
				Instance = def.Instance,
				DisplayName = def.DisplayName
			}));
			Selected = Counters.First();
		}
	}
}
