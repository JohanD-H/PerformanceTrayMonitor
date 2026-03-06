using PerformanceTrayMonitor.Common;
using PerformanceTrayMonitor.Configuration;
using PerformanceTrayMonitor.Models;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
using System.Windows.Threading;

// --------------------------------------------
// Configuration window
// --------------------------------------------
namespace PerformanceTrayMonitor.ViewModels
{
	public class ConfigViewModel : BaseViewModel
	{
		private readonly MainViewModel _main;
		
		private bool _initializingEditor;
		public ObservableCollection<CounterViewModel> Counters { get; } = new();
		public ObservableCollection<string> Categories { get; } = new();
		public ObservableCollection<string> CountersInCategory { get; } = new();
		public ObservableCollection<string> Instances { get; } = new();
		public ObservableCollection<string> AvailableIconSets { get; } =
			new ObservableCollection<string>(
			IconSetConfig.IconSets.Keys.OrderBy(x => x)
		);
		public event Action? RequestClose;
		private bool _hasPendingEdits;
		private RelayCommand ApplyCmd => (RelayCommand)ApplyCommand;
		private RelayCommand CancelCmd => (RelayCommand)CancelCommand;
		private RelayCommand RemoveCmd => (RelayCommand)RemoveCommand;
		private RelayCommand ResetCmd => (RelayCommand)ResetCommand;
		public Func<bool>? ConfirmReset { get; set; } // <-- MUST be above InitializeCommands()

		public ICommand AddCommand { get; private set; }
		public ICommand ApplyCommand { get; private set; }
		public ICommand CancelCommand { get; private set; }
		public ICommand RemoveCommand { get; private set; }
		public ICommand ResetCommand { get; private set; }
		public ICommand SaveCommand { get; private set; }
		public CounterEditorViewModel Editor { get; }

		private CounterViewModel? _selected;
		public CounterViewModel? Selected
		{
			get => _selected;
			set
			{
				if (_selected == value)
					return;

				_selected = value;
				OnPropertyChanged();

				if (_selected == null)
				{
					CountersInCategory.Clear();
					Instances.Clear();
					return;
				}
				_initializingEditor = true;

				// Temporarily disable reactive logic
				Editor.PropertyChanged -= Editor_PropertyChanged;

				// Now load the editor (this triggers SelectedCategory/Counter)
				Editor.LoadFrom(_selected);

				// Re-enable reactive logic
				Editor.PropertyChanged += Editor_PropertyChanged;

				HasPendingEdits = false;
				RemoveCmd.RaiseCanExecuteChanged();
				ApplyCmd.RaiseCanExecuteChanged();

				// Let WPF finish binding on the next dispatcher tick
				Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
				{
					_initializingEditor = false;
				}));
			}
		}

		private void InitializeCommands()
		{
			AddCommand = new RelayCommand(_ => AddNewCounterFromEditor());
			ApplyCommand = new RelayCommand(_ => ApplyEditorToSelected(), _ => Selected != null && HasPendingEdits);
			CancelCommand = new RelayCommand(_ => CancelEdits(), _ => Selected != null && HasPendingEdits);
			RemoveCommand = new RelayCommand(_ => RemoveSelected(), _ => Selected != null);
			ResetCommand = new RelayCommand(_ => { if (ConfirmReset == null || ConfirmReset()) ResetToDefaults(); }, _ => !IsAtDefaults());
			SaveCommand = new RelayCommand(_ => Save());
		}

		public ConfigViewModel(SettingsOptions settings, MainViewModel main)
		{
			Editor = new CounterEditorViewModel(this);

			Log.Debug($"THIS VM = {GetHashCode()}");

			_main = main;

			foreach (var dto in settings.Counters)
			{
				var cs = new CounterSettings
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
				};

				Counters.Add(new CounterViewModel(cs));
			}

			foreach (var cat in PerformanceCounterCategory.GetCategories().OrderBy(c => c.CategoryName))
				Categories.Add(cat.CategoryName);

			Editor.PropertyChanged += Editor_PropertyChanged;

			InitializeCommands();

			if (Counters.Any())
				Selected = Counters.First();
			else
				Editor.LoadDefaults();
		}

		private void Editor_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			Log.Debug($"Editor_PropertyChanged: _initializingEditor={_initializingEditor}");

			if (_initializingEditor)
				return;

			HasPendingEdits = true;

#if DEBUG
			if (e.PropertyName is nameof(Editor.SelectedCategory) or nameof(Editor.SelectedCounter) or nameof(Editor.SelectedInstance))
			{
				Log.Debug($"Editor_PropertyChanged: Cat='{Editor.Category}', Ctr='{Editor.Counter}', Inst='{Editor.Instance}'");
			}
#endif
		}

		private bool IsAtDefaults()
		{
			if (Counters.Count != 1)
				return false;

			var dto = new DefaultSettingsProvider().CreateDefaultCounter();
			var def = CreateSettingsFromDto(dto);
			var cur = Counters[0].Settings;

			return
				cur.Category == def.Category &&
				cur.Counter == def.Counter &&
				cur.Instance == def.Instance &&
				cur.DisplayName == def.DisplayName &&
				cur.Min == def.Min &&
				cur.Max == def.Max &&
				cur.ShowInTray == def.ShowInTray &&
				cur.IconSet == def.IconSet;
		}

		private void AddNewCounterFromEditor()
		{
			var settings = Editor.ToSettings();
			Log.Debug($"settings.DisplayName = {settings.DisplayName}");
			settings.Id = Guid.NewGuid();

			var vm = new CounterViewModel(settings);
			Counters.Add(vm);
			Selected = vm;

			ResetCmd.RaiseCanExecuteChanged();
		}

		private void ApplyEditorToSelected()
		{
			if (Selected == null)
				return;

			var settings = Editor.ToSettings();

			Log.Debug($"settings.DisplayName = {settings.DisplayName}");
			Selected.Category = settings.Category;
			Selected.Counter = settings.Counter;
			Selected.Instance = settings.Instance;
			Selected.DisplayName = settings.DisplayName;
			Selected.Min = settings.Min;
			Selected.Max = settings.Max;
			Selected.ShowInTray = settings.ShowInTray;
			Selected.IconSet = settings.IconSet;

			HasPendingEdits = false;
			RefreshCommandStates();
		}

		private void CancelEdits()
		{
			if (Selected != null)
			{
				Editor.PropertyChanged -= Editor_PropertyChanged;

				Editor.LoadFrom(Selected);

				Log.Debug($"Editor.DisplayName = {Editor.DisplayName},Editor.Category = {Editor.Category}, Editor.Counter = {Editor.Counter}");

				Editor.PropertyChanged += Editor_PropertyChanged;

				HasPendingEdits = false;
				RefreshCommandStates();
			}
		}

		private void RefreshCommandStates()
		{
			ApplyCmd.RaiseCanExecuteChanged();
			CancelCmd.RaiseCanExecuteChanged();
			ResetCmd.RaiseCanExecuteChanged();
		}

		private void RemoveSelected()
		{
			if (Selected == null)
				return;

			var index = Counters.IndexOf(Selected);
			Counters.Remove(Selected);

			if (Counters.Count == 0)
			{
				Selected = null;
				Editor.LoadDefaults();

			}
			else if (index < Counters.Count)
				Selected = Counters[index];
			else
				Selected = Counters.Last();

			ResetCmd.RaiseCanExecuteChanged();
		}

		private CounterSettings CreateSettingsFromDto(CounterSettingsDto dto)
		{
			Log.Debug($"dto.DisplayName = {dto.DisplayName},dto.Category = {dto.Category}, dto.Counter = {dto.Counter}");
			return new CounterSettings
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
			};
		}

		internal void ResetToDefaults()
		{
			Counters.Clear();

			var dto = new DefaultSettingsProvider().CreateDefaultCounter();
			Counters.Add(new CounterViewModel(CreateSettingsFromDto(dto)));

			Selected = Counters.FirstOrDefault();

			HasPendingEdits = false;
			ApplyCmd.RaiseCanExecuteChanged();
			CancelCmd.RaiseCanExecuteChanged();
			ResetCmd.RaiseCanExecuteChanged();
		}

		private void Save()
		{
			// Always apply editor changes to the selected counter
			if (Selected != null)
				ApplyEditorToSelected();

#if DEBUG
			Log.Debug("SAVE SNAPSHOT:");
			foreach (var c in Counters)
				Log.Debug($" {c.DisplayName} | {c.Category} | {c.Counter} | {c.Instance} | Show={c.ShowInTray}");
#endif

			// Build a new SettingsOptions using the edited counters
			var newSettings = new SettingsOptions(
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
			);

			// Push updated settings into MainViewModel
			_main.ReplaceSettings(newSettings);

			// Save to disk
			SettingsStore.Save(newSettings);

			// Close window
			RequestClose?.Invoke();

			Cleanup();
		}

		private void Cleanup()
		{
			Editor.PropertyChanged -= Editor_PropertyChanged;
		}


		public void LoadCountersForCategory(string category)
		{
			Log.Debug($"category = '{category}'");

			CountersInCategory.Clear();

			try
			{
				var cat = new PerformanceCounterCategory(category);

				if (cat.GetInstanceNames().Length == 0)
				{
					foreach (var c in cat.GetCounters().Select(c => c.CounterName).Distinct().OrderBy(x => x))
					{
						CountersInCategory.Add(c);
					}

					Instances.Clear();
					Instances.Add("");
				}
				else
				{
					var instances = cat.GetInstanceNames().OrderBy(x => x).ToArray();
					Instances.Clear();
					foreach (var inst in instances)
					{
						Instances.Add(inst);
					}

					var allCounters = instances
						.SelectMany(inst => cat.GetCounters(inst))
						.Select(c => c.CounterName)
						.Distinct()
						.OrderBy(x => x);

					foreach (var c in allCounters)
					{
						CountersInCategory.Add(c);
					}
				}
			}
			catch (Exception ex)
			{
				Log.Debug($"{ex}Failed to load counters for category '{category}'");
			}
		}

		public void LoadInstancesForCounter(string category, string counter)
		{
			Log.Debug($"category = '{category}', counter =  '{counter}'");

			Instances.Clear();

			try
			{
				var cat = new PerformanceCounterCategory(category);
				var instances = cat.GetInstanceNames();

				if (instances.Length == 0)
				{
					Instances.Add("");
				}
				else
				{
					foreach (var inst in instances.OrderBy(x => x))
						Instances.Add(inst);
				}
			}
			catch (Exception ex)
			{
				Log.Debug($"{ex}Failed to load instances for category/counter '{category}'/'{counter}'");
			}
		}

		public void RefreshSelectionsAfterLoad()
		{
			if (Selected == null || Editor == null)
				return;

			Log.Debug("[WIN] RefreshSelectionsAfterLoad: Re-applying saved selections");
			_initializingEditor = true;

			// Reapply the saved values AFTER the UI has populated its ComboBoxes
			using (var _ = Editor.BeginSuppress())
			{
				Editor.SelectedCategory = Selected.Category;
				Editor.SelectedCounter = Selected.Counter;
				Editor.SelectedInstance = Selected.Instance;
			}

			// Trigger UI update
			Editor.NotifyAll();

			// keep initializing ON until NotifyAll finishes rebinding
			HasPendingEdits = false;

			// Defer ending initialization
			Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
			{
				_initializingEditor = false;
			}));
		}

		public bool HasPendingEdits
		{
			get => _hasPendingEdits;
			private set
			{
				Log.Debug($"HasPendingEdits changing: {_hasPendingEdits} -> {value}");
				if (_hasPendingEdits != value)
				{
					_hasPendingEdits = value;
					OnPropertyChanged();

					ApplyCmd.RaiseCanExecuteChanged();
					CancelCmd.RaiseCanExecuteChanged();
				}
			}
		}

	}

	public class RelayCommand : ICommand
	{
		private readonly Action<object?> _execute;
		private readonly Predicate<object?>? _canExecute;

		public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
		{
			_execute = execute;
			_canExecute = canExecute;
		}

		public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
		public void Execute(object? parameter) => _execute(parameter);

		public event EventHandler? CanExecuteChanged;
		public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
	}
}