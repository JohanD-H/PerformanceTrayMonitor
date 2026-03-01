using PerformanceTrayMonitor.Configuration;
using PerformanceTrayMonitor.Managers;
using PerformanceTrayMonitor.Models;
using PerformanceTrayMonitor.Views;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Windows.Input;

// --------------------------------------------
// Configuration window
// --------------------------------------------
namespace PerformanceTrayMonitor.ViewModels
{
	public class ConfigViewModel : BaseViewModel
	{
		private readonly MainViewModel _main;

		public ObservableCollection<CounterViewModel> Counters { get; } = new();
		public ObservableCollection<string> Categories { get; } = new();
		public ObservableCollection<string> CountersInCategory { get; } = new();
		public ObservableCollection<string> Instances { get; } = new();
		public ObservableCollection<string> AvailableIconSets { get; } =
			new ObservableCollection<string>(
			IconSetConfig.IconSets.Keys.OrderBy(x => x)
		);

		public event Action? RequestClose;

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

				// Temporarily disable reactive logic
				Editor.PropertyChanged -= Editor_PropertyChanged;

				// Load lists FIRST
				LoadCountersForCategory(_selected.Category);
				LoadInstancesForCounter(_selected.Category, _selected.Counter);

				// Now load the editor (this triggers SelectedCategory/Counter)
				Editor.LoadFrom(_selected);

				// Re-enable reactive logic
				Editor.PropertyChanged += Editor_PropertyChanged;

				(RemoveCommand as RelayCommand)?.RaiseCanExecuteChanged();
				(ApplyCommand as RelayCommand)?.RaiseCanExecuteChanged();
			}
		}

		public CounterEditorViewModel Editor { get; }

		public ConfigViewModel()
		{
			Editor = new CounterEditorViewModel(this);
		}

		public ICommand AddCommand { get; }
		public ICommand ApplyCommand { get; }
		public ICommand CancelCommand { get; }
		public ICommand RemoveCommand { get; }
		public ICommand ResetCommand { get; }
		public ICommand SaveCommand { get; }

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

			AddCommand = new RelayCommand(_ => AddFromEditor());
			ApplyCommand = new RelayCommand(_ => ApplyToSelected(), _ => Selected != null);
			CancelCommand = new RelayCommand(_ => CancelEdits(), _ => Selected != null);
			RemoveCommand = new RelayCommand(_ => Remove(), _ => Selected != null);
			ResetCommand = new RelayCommand(_ => ResetToDefaults());
			SaveCommand = new RelayCommand(_ => Save());

			if (Counters.Any())
				Selected = Counters.First();
			else
				Editor.LoadDefaults();
		}

		private void Editor_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			// CATEGORY CHANGED
			if (e.PropertyName == nameof(Editor.Category))
			{
				LoadCountersForCategory(Editor.Category);

				// Only auto-select if the category actually exists
				if (CountersInCategory.Any())
				{
					Log.Debug($"Counters: Editor.Category = {Editor.Category}, Editor.Counter = {Editor.Counter}");
					if (!CountersInCategory.Contains(Editor.Counter))
						Editor.Counter = CountersInCategory.First();

					LoadInstancesForCounter(Editor.Category, Editor.Counter);

					if (Instances.Any() && !Instances.Contains(Editor.Instance))
						Editor.Instance = Instances.First();
				}
			}

			// COUNTER CHANGED
			if (e.PropertyName == nameof(Editor.Counter))
			{
				Log.Debug($"Instances: Editor.Category = {Editor.Category}, Editor.Counter = {Editor.Counter}");
				LoadInstancesForCounter(Editor.Category, Editor.Counter);

				if (Instances.Any() && !Instances.Contains(Editor.Instance))
					Editor.Instance = Instances.First();
			}
		}

		private void AddFromEditor()
		{
			var settings = Editor.ToSettings();
			settings.Id = Guid.NewGuid();

			var vm = new CounterViewModel(settings);
			Counters.Add(vm);
			Selected = vm;
		}

		private void ApplyToSelected()
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
		}

		private void CancelEdits()
		{
			if (Selected != null)
			{
				Editor.PropertyChanged -= Editor_PropertyChanged;

				Editor.LoadFrom(Selected);

				Log.Debug($"Editor.Category = {Editor.Category}, Editor.Counter = {Editor.Counter}");
				LoadCountersForCategory(Editor.Category);
				LoadInstancesForCounter(Editor.Category, Editor.Counter);

				Editor.PropertyChanged += Editor_PropertyChanged;
			}
		}

		private void Remove()
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
		}

		private CounterSettings CreateSettingsFromDto(CounterSettingsDto dto)
		{
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

		private void ResetToDefaults()
		{
			Counters.Clear();

			var dto = new DefaultSettingsProvider().CreateDefaultCounter();
			Counters.Add(new CounterViewModel(CreateSettingsFromDto(dto)));

			Selected = Counters.FirstOrDefault();
		}

		private void Save()
		{
			// Always apply editor changes to the selected counter
			if (Selected != null)
				ApplyToSelected();

			Log.Debug("SAVE SNAPSHOT:");
			foreach (var c in Counters)
				Log.Debug($" {c.DisplayName} | {c.Category} | {c.Counter} | {c.Instance} | Show={c.ShowInTray}");

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
				Log.Debug(ex, $"Failed to load counters for category '{category}'");
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
				Log.Debug(ex, $"Failed to load instances for category/counter '{category}'/'{counter}'");
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
