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

namespace PerformanceTrayMonitor.ViewModels
{
	public class ConfigViewModel : BaseViewModel
	{
		private readonly MainViewModel _main;

		public ObservableCollection<CounterViewModel> Counters { get; } = new();
		public ObservableCollection<string> Categories { get; } = new();
		public ObservableCollection<string> CountersInCategory { get; } = new();
		public ObservableCollection<string> Instances { get; } = new();

		public ObservableCollection<string> Modes { get; } =
			new(CounterConfig.Modes);

		public ObservableCollection<string> IconSets { get; } =
			new(CounterConfig.IconSets);

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

        if (_selected != null)
        {
            // Temporarily disable reactive logic while we programmatically load
            Editor.PropertyChanged -= Editor_PropertyChanged;

            // Load editor from selected VM
            Editor.LoadFrom(_selected);

            Log.Debug($"Editor.Category = {Editor.Category}, Editor.Counter = {Editor.Counter}");

            // Populate lists for this category
            LoadCountersForCategory(Editor.Category);
            LoadInstancesForCounter(Editor.Category, Editor.Counter);

            // Ensure Counter is valid for this category
            if (CountersInCategory.Any() && !CountersInCategory.Contains(Editor.Counter))
            {
                Log.Debug($"Coercing Editor.Counter from '{Editor.Counter}' to first in category '{Editor.Category}'");
                Editor.Counter = CountersInCategory.First();
            }

            // Ensure Instance is valid for this category
            if (Instances.Any() && !Instances.Contains(Editor.Instance))
            {
                Log.Debug($"Coercing Editor.Instance from '{Editor.Instance}' to first in category '{Editor.Category}'");
                Editor.Instance = Instances.First();
            }

            // Re-enable for real user edits
            Editor.PropertyChanged += Editor_PropertyChanged;
        }

        (RemoveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ApplyCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }
}

		public CounterEditorViewModel Editor { get; } = new();

		public ICommand AddCommand { get; }
		public ICommand ApplyCommand { get; }
		public ICommand CancelCommand { get; }
		public ICommand RemoveCommand { get; }
		public ICommand ResetCommand { get; }
		public ICommand SaveCommand { get; }

		public ConfigViewModel(List<CounterSettingsDto> snapshot, MainViewModel main)
		{
			Log.Debug($"THIS VM = {GetHashCode()}");

			_main = main;

			// Load counters from snapshot
			foreach (var dto in snapshot)
			{
				var settings = new CounterSettings
				{
					Id = dto.Id,
					Category = dto.Category,
					Counter = dto.Counter,
					Instance = dto.Instance,
					DisplayName = dto.DisplayName,
					Min = dto.Min,
					Max = dto.Max,
					Mode = dto.Mode,
					ShowInTray = dto.ShowInTray,
					IconSet = dto.IconSet
				};

				Counters.Add(new CounterViewModel(settings));
			}

			// Load categories
			foreach (var cat in PerformanceCounterCategory.GetCategories().OrderBy(c => c.CategoryName))
			{
				//Log.Debug($"Adding: cat.CategoryName = {cat.CategoryName}");
				Categories.Add(cat.CategoryName);
			}

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
			Selected.Mode = settings.Mode;
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

		private void ResetToDefaults()
		{
			Counters.Clear();
			Editor.LoadDefaults();
		}

		private void Save()
		{
			// Always apply editor changes to the selected counter
			if (Selected != null)
				ApplyToSelected();

			Log.Debug("SAVE SNAPSHOT:");
			foreach (var c in Counters)
				Log.Debug($" {c.DisplayName} | {c.Category} | {c.Counter} | {c.Instance} | Show={c.ShowInTray}");

			_main.ReplaceCounters(
				Counters.Select(c => new CounterSettingsDto
				{
					Id = c.Settings.Id,
					Category = c.Category,
					Counter = c.Counter,
					Instance = c.Instance,
					DisplayName = c.DisplayName,
					Min = c.Min,
					Max = c.Max,
					Mode = c.Mode,
					ShowInTray = c.ShowInTray,
					IconSet = c.IconSet
				}).ToList()
			);

			SettingsStore.Save(_main.GetSettingsSnapshot());
			RequestClose?.Invoke();
		}

		private void LoadCountersForCategory(string category)
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

		private void LoadInstancesForCounter(string category, string counter)
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
