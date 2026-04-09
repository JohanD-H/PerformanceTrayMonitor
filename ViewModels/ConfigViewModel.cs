using PerformanceTrayMonitor.Common;
using PerformanceTrayMonitor.Configuration;
using PerformanceTrayMonitor.Extensions;
using PerformanceTrayMonitor.Models;
using PerformanceTrayMonitor.Properties;
using PerformanceTrayMonitor.Settings;
using PerformanceTrayMonitor.Tray;
using PerformanceTrayMonitor.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PerformanceTrayMonitor.ViewModels
{
	public class ConfigViewModel : BaseViewModel
	{
		private readonly MainViewModel _main;

		private CounterViewModel? _selected;

		internal CounterViewModel? _pendingRemoval;

		public event Action LoadSelectedCompleted;

		// ============================================================
		//  CLEAN STATE MODEL
		// ============================================================

		/// True when the configuration as a whole has unsaved changes.
		/// This drives Save/Cancel.
		public bool GlobalEditsPending
		{
			get => _globalEditsPending;
			private set
			{
				if (_globalEditsPending == value) return;
				_globalEditsPending = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(AnyPendingEdits));
				RefreshCommandStates();
			}
		}
		private bool _globalEditsPending;

		/// True when the editor UI has unsaved edits for the currently selected metric.
		/// This drives Apply/Discard.
		public bool EditorPendingEdits
		{
			get => _editorPendingEdits;
			private set
			{
				if (_editorPendingEdits == value) return;
				_editorPendingEdits = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(AnyPendingEdits));
				RefreshCommandStates();
			}
		}
		private bool _editorPendingEdits;
		
		// Helper
		public void MarkEditorDirty()
		{
			if (SuppressEditorChanges || _isSelectionLoadInProgress)	// Safety gates
				return;

			EditorPendingEdits = true;
			Log.Debug($"MarkEditorDirty: EditorPendingEdits = {EditorPendingEdits}");
		}

		/// True when the editor UI has unsaved changes or edits for the currently selected metric.
		/// This drives Details in the UI.
		public bool AnyPendingEdits => EditorPendingEdits || GlobalEditsPending;

		/// True when the entire configuration matches the default template.
		/// Drives the Reset button.
		public bool IsAtDefaultConfiguration
		{
			get => _isAtDefaultConfiguration;
			private set
			{
				if (_isAtDefaultConfiguration == value) return;
				_isAtDefaultConfiguration = value;
				OnPropertyChanged();
				RefreshCommandStates();
			}
		}
		private bool _isAtDefaultConfiguration;

		// ============================================================
		//  INTERNAL SHIELDS (minimal, intentional)
		// ============================================================

		/// UI loading shield (unchanged).
		private bool _isLoading;
		public bool IsLoading
		{
			get => _isLoading;
			private set
			{
				_isLoading = value;
				OnPropertyChanged();
				// Flush pending preview updates when loading finishes
				if (!_isLoading)
				{
					UpdateDynamicPreview();
				}
			}
		}

		private int _loadingCount;

		public bool SuppressEditorChanges;

		public bool IsBusy => _loadingCount > 0;	// XAML binding Updating.. overlay

		internal void BeginLoading()
		{
			_loadingCount++;
			IsLoading = true;
			OnPropertyChanged(nameof(IsBusy));
			OnPropertyChanged(nameof(StatusText));
		}

		internal void EndLoading()
		{
			_loadingCount--;
			if (_loadingCount <= 0)
			{
				_loadingCount = 0;
				IsLoading = false;
			}
			OnPropertyChanged(nameof(IsBusy));
			OnPropertyChanged(nameof(StatusText));
			RefreshCommandStates();
		}

		// ============================================================
		//  SETTINGS + COLLECTIONS
		// ============================================================

		private SettingsOptions? _globalSettings;
		public SettingsOptions? GlobalSettings
		{
			get => _globalSettings;
			set { _globalSettings = value; OnPropertyChanged(); }
		}

		public ObservableCollection<CounterViewModel> Counters { get; } = new();
		public ObservableCollection<string> Categories { get; } = new();
		public ObservableCollection<string> CountersInCategory { get; private set; } = new();
		public ObservableCollection<string> Instances { get; private set; } = new();

		// XAML - ConfigWindow
		public ObservableCollection<string> AvailableIconSets { get; } = new(IconSetConfig.IconSets.Keys.OrderBy(x => x));

		public CounterEditorViewModel Editor { get; }

		internal bool _isSelectionLoadInProgress;
		internal bool IsSelectionLoadInProgress
		{
			get => _isSelectionLoadInProgress;
			private set
			{
				if (_isSelectionLoadInProgress == value)
					return;

				_isSelectionLoadInProgress = value;
				OnPropertyChanged();

				// Flush pending preview updates when selection load finishes
				if (!_isSelectionLoadInProgress)
				{
					UpdateDynamicPreview();
				}
			}
		}

		public Action? RequestClose { get; set; }

		private DispatcherTimer? _previewTimer;

		private readonly Random _random = new();
		public Func<bool>? ConfirmReset { get; set; }
		public Func<bool>? ConfirmCancel { get; set; }
		public Func<bool>? ConfirmClose { get; set; }

		public CancellationTokenSource _cts = new();
		public CancellationTokenSource? _instanceLoadCts;
		public void CancelAllWork()
		{
			_cts.Cancel();
		}

		// The TrayIconCount needs to reflect intended  count, so that it reflects what save will do, this means that on cancel we also need to adjust!
		//public int TrayIconCount => Metrics.Count(c => c.ShowInTray);
		public int TrayIconCount => Metrics.Count(c => c.ShowInTray && !c.IsPendingRemoval);
		bool EnforceTrayLimit(CounterViewModel? vm, bool desiredShowInTray)
		{
			//bool wasInTray = Selected.Settings.ShowInTray;
			//bool wasInTray = Selected.Settings.ShowInTray && !Selected.IsPendingRemoval;
			bool wasInTray = vm != null && vm.Settings.ShowInTray && !vm.IsPendingRemoval;

			//Log.Debug($"EnforceTrayLimit: TrayIconCount = {TrayIconCount}, wasInTray = {wasInTray}, desiredShowInTray = {desiredShowInTray}");

			// If the metric was already in the tray, allow it even if full
			if (wasInTray)
			{
				//Log.Debug($"EnforceTrayLimit: result = {desiredShowInTray}");
				return desiredShowInTray;
			}

			// If the metric is new and wants to be in the tray, enforce limit
			if (desiredShowInTray && TrayIconCount >= TrayIconConfig.MaxCounterTrayIcons)
			{
				//Log.Debug($"EnforceTrayLimit: result = False");
				return false;
			}
			/*
			//int currentCount = Metrics.Count(c => c.ShowInTray && !c.IsPendingRemoval);
			int futureCount = TrayIconCount
				- (wasInTray ? 1 : 0)
				+ (desiredShowInTray ? 1 : 0);
			*/

			//Log.Debug($"EnforceTrayLimit: result = {desiredShowInTray}");
			return desiredShowInTray;
		}

		public string TrayIconCountDisplay =>
			$"Tray icons: {TrayIconCount}/{TrayIconConfig.MaxCounterTrayIcons}";

		private BitmapSource? _trayPreviewImage;
		public BitmapSource TrayPreviewImage
		{
			get => _trayPreviewImage;
			private set
			{
				if (_trayPreviewImage != value)
				{
					_trayPreviewImage = value;
					OnPropertyChanged();
				}
			}
		}

		public string StatusText => IsLoading ? "Loading…" : "Ready";

		// Commands
		//public ICommand? ApplyCommand { get; private set; }
		public ICommand? AddCommand { get; private set; }
		public ICommand? CopyCommand { get; private set; }
		public ICommand? CancelCommand { get; private set; }
		public ICommand? UpdateCommand { get; private set; }
		public ICommand? EditCommand { get; private set; }
		public ICommand? OpenDebugIconWindowCommand { get; private set; }
		public ICommand? RemoveCommand { get; private set; }
		public ICommand? ResetCommand { get; private set; }
		public ICommand? SaveCommand { get; private set; }
		public ICommand? CloseCommand { get; private set; }
		public ICommand? ShowMinMaxInfoCommand { get; private set;  }

		public ObservableCollection<CounterViewModel> Metrics { get; }
			= new ObservableCollection<CounterViewModel>();
		public int MetricsCount => Metrics.Count;
		private List<CounterSettingsDto> _lastSavedMetricsSnapshot = new();
		public IReadOnlyList<CounterSettingsDto> LastSavedMetricsSnapshot
			=> _lastSavedMetricsSnapshot;

		public Action<CounterViewModel>? OnMetricPendingRemoval { get; set; }
		public Action<CounterViewModel>? OnMetricCopied { get; set; }
		public Action<CounterViewModel>? OnMetricAdded { get; set; }
		public Action<CounterViewModel>? OnMetricUpdated { get; set; }

		internal readonly ShadowMetricState _shadow = new ShadowMetricState();

		public BitmapSource[]? IconSetPreviewFrames { get; set; }

		public Window? OwnerWindow { get; set; }
		// XAML binding!
		//public bool CanShowInTray =>
		//	Selected?.ShowInTray == true ||
		//	TrayIconCount < TrayIconConfig.MaxCounterTrayIcons;
		public bool CanShowInTray
		{
			get
			{
				bool isEditingExisting = Selected?.Id == Editor.Id;

				if (isEditingExisting)
				{
					// If this metric already has a tray icon, always allow toggling it off.
					if (Selected.ShowInTray)
						return true;

					// Otherwise, enforce the limit.
					return TrayIconCount < TrayIconConfig.MaxCounterTrayIcons;
				}

				// Adding a new metric → enforce the limit.
				return TrayIconCount < TrayIconConfig.MaxCounterTrayIcons;
			}
		}

		public ConfigViewModel(SettingsOptions settings, MainViewModel main)
		{
			GlobalSettings = settings;
			_main = main;
			Editor = new CounterEditorViewModel(this);

			var cats = PerformanceCounterCategory
				.GetCategories()
				.Select(c => c.CategoryName)
				.OrderBy(x => x);

			foreach (var cat in cats)
				Categories.Add(cat);

			// Load existing metrics from GlobalSettings
			foreach (var metricDto in GlobalSettings.Metrics)
			{
				var vm = new CounterViewModel(metricDto);
				vm.PropertyChanged += Counter_PropertyChanged; 
				Metrics.Add(vm);
			}

			InitializeCommands();

			// Defer selection until UI is ready
			Application.Current.Dispatcher.BeginInvoke(new Action(() =>
			{
				if (Metrics.Any())
					Selected = Metrics.First();
				else
					Editor.LoadDefaults();
			}), DispatcherPriority.ContextIdle);

			Editor.PropertyChanged += Editor_PropertyChanged;

			// If LoadIntoViewModel just re-applies settings, pass GlobalSettings, not the ctor parameter
			LoadIntoViewModel(GlobalSettings);

			// Initial snapshot
			SaveSnapshot();
		}

		public CounterViewModel? Selected
		{
			get => _selected;
			set
			{
				if (_selected == value)
					return;

				_selected = value;
				OnPropertyChanged();

				RefreshCommandStates();
			}
		}

		public sealed class ShadowMetricState
		{
			public string Category = "";
			public string Counter = "";
			public string Instance = "";

			public List<string> CountersInCategory { get; } = new();
			public List<string> Instances { get; } = new();

			internal void Reset()
			{
				Category = "";
				Counter = "";
				Instance = "";

				CountersInCategory.Clear();
				Instances.Clear();
			}

		}

		internal bool _isCommittingShadow;

		private void CommitShadowToEditor()
		{
			_isCommittingShadow = true;
			Editor._suppressEditorSetters = true;

			try
			{
				//Log.Debug($"CommitShadowToEditor: Category = {_shadow.Category}, Counter = {_shadow.Counter}, Instance = {_shadow.Instance}");
				Editor.SelectedCategory = _shadow.Category;
				Editor.SelectedCounter = _shadow.Counter;
				Editor.SelectedInstance = _shadow.Instance;
			}
			finally
			{
				Editor._suppressEditorSetters = false;
				_isCommittingShadow = false;
			}
		}

		public void ResetEditorDirtyState()
		{
			EditorPendingEdits = false;
			IsAtDefaultConfiguration = CheckIfDefault();
		}

		private void Editor_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			//Log.Debug($"Editor_PropertyChanged: property = {e.PropertyName}, " +
			//		  $"IsLoading = {IsLoading}, IsSelectionLoadInProgress = {IsSelectionLoadInProgress}");
			// ------------------------------------------------------------
			// Ignore ALL changes during programmatic loads
			// ------------------------------------------------------------
			if (IsLoading || IsSelectionLoadInProgress || SuppressEditorChanges)
			{
				//Log.Debug($"Editor_PropertyChanged: EARLY EXIT");
				return;
			}

			// ------------------------------------------------------------
			// Real user edits → mark editor dirty
			// ------------------------------------------------------------
			//EditorPendingEdits = true;
			IsAtDefaultConfiguration = false;
			//Log.Debug($"Editor_PropertyChanged: EditorPendingEdits = {EditorPendingEdits}");

			// ------------------------------------------------------------
			// Handle special cases
			// ------------------------------------------------------------
			//var defaults = new DefaultSettingsProvider().CreateDefaultCounter();

			switch (e.PropertyName)
			{
				case nameof(Editor.SelectedCategory):
					// Identity changed → force new metric
					//Log.Debug($"Editor_PropertyChanged, Editor Category changed {Editor.SelectedCategory}");
					//Editor.Id = Guid.Empty;  <-- No longer used, new UI setup does not need it and it may harm!
					break;

				case nameof(Editor.SelectedCounter):
				case nameof(Editor.SelectedInstance):
					break;

				case nameof(Editor.ShowIconSetSelector):
				case nameof(Editor.IconSet):
					LoadIconSetPreviewFrames(Editor.IconSet);
					break;

				case nameof(Editor.TrayAccentColor):
				case nameof(Editor.TrayBackgroundColor):
				case nameof(Editor.AutoTrayBackground):
				case nameof(Editor.UseTextTrayIcon):
				case nameof(Editor.ShowInTray):
				case nameof(Editor.ShowBackgroundColorPicker):
				case nameof(Editor.CurrentValue):
					// No extra action needed — editor is already marked dirty
					break;
			}

			// ------------------------------------------------------------
			// Update preview + status bar
			// ------------------------------------------------------------
			//Log.Debug($"Editor_PropertyChanged, Update preview + Statusbar!");
			UpdateDynamicPreview();
			OnPropertyChanged(nameof(MetricsCount));
			OnPropertyChanged(nameof(StatusText));
		}

		public async Task ApplySelectedFromEditorAsync()
		{
			if (SuppressEditorChanges || _isSelectionLoadInProgress || IsLoading)
			{
				Log.Debug("[Pipeline] ApplySelectedFromEditorAsync  — EARLY RETURN!");
				return;
			}

			Log.Debug("[Pipeline] ApplySelectedFromEditorAsync running — THIS WILL OVERWRITE SELECTION");

			IsSelectionLoadInProgress = true;
			BeginLoading();
			Editor._suppressEditorSetters = true;

			try
			{
				//
				// Load counters for the selected category
				//
				//Log.Debug($"ApplySelectedAsync: loading counters for Category = {_shadow.Category}");

				await Task.Run(async () =>
				{
					var counters = await LoadCountersCoreAsync(_shadow.Category, _cts.Token);

					_shadow.CountersInCategory.Clear();
					_shadow.CountersInCategory.AddRange(counters);
				});

				//
				// Auto-select counter BEFORE loading instances
				//
				string beforeCounter = string.IsNullOrEmpty(_shadow.Counter) ? "Null" : _shadow.Counter;
				//Log.Debug($"ApplySelectedAsync: auto-select Counter (before) = {beforeCounter}");

				if (string.IsNullOrWhiteSpace(_shadow.Counter) ||
					!_shadow.CountersInCategory.Contains(_shadow.Counter))
				{
					if (_shadow.CountersInCategory.Count > 0)
						_shadow.Counter = _shadow.CountersInCategory[0];
				}

				//Log.Debug($"ApplySelectedAsync: auto-select Counter (after) = {_shadow.Counter}");

				//
				// Load instances for the selected counter
				//
				//Log.Debug($"ApplySelectedAsync: loading instances for Category={_shadow.Category}, Counter={_shadow.Counter}");

				await Task.Run(async () =>
				{
					var instances = await LoadInstancesCoreAsync(_shadow.Category, _shadow.Counter, _cts.Token);

					_shadow.Instances.Clear();
					_shadow.Instances.AddRange(instances);
				});

				//
				// Auto-select instance
				//
				string beforeInstance = string.IsNullOrEmpty(_shadow.Instance) ? "Null" : _shadow.Instance;
				//Log.Debug($"ApplySelectedAsync: auto-select Instance (before) = {beforeInstance}");

				if (string.IsNullOrWhiteSpace(_shadow.Instance) ||
					!_shadow.Instances.Contains(_shadow.Instance))
				{
					if (_shadow.Instances.Count > 0)
						_shadow.Instance = _shadow.Instances[0];
				}

				//Log.Debug($"ApplySelectedAsync: auto-select Instance (after) = {_shadow.Instance}");

				//
				// Push lists to UI
				//
				await Application.Current.Dispatcher.InvokeAsync(() =>
				{
					Log.Debug("[Pipeline] ApplySelectedFromEditorAsync Replacing CountersInCategory collection");

					// Update Editor.CountersInCategory
					Editor.CountersInCategory.Clear();
					foreach (var c in _shadow.CountersInCategory)
						Editor.CountersInCategory.Add(c);
					OnPropertyChanged(nameof(Editor.CountersInCategory));

					Editor.Instances.Clear();
					foreach (var inst in _shadow.Instances)
						Editor.Instances.Add(inst);
					OnPropertyChanged(nameof(Editor.Instances));
				});

				//
				// Commit shadow → UI
				//
				//Log.Debug("ApplySelectedAsync: committing shadow to editor");
				CommitShadowToEditor();
			}
			finally
			{
				Editor._suppressEditorSetters = false;
				EndLoading();
				IsSelectionLoadInProgress = false;

				//Log.Debug($"ApplySelectedFromEditorAsync: Editor Iconset = {Editor.IconSet}");
				LoadIconSetPreviewFrames(Editor.IconSet);
				UpdateDynamicPreview();
			}
		}

		private T SafePerf<T>(Func<T> func, string context)
		{
			try
			{
				return func();
			}
			catch
			{
				//Log.Error(ex, $"PerfCounter error: {context}");
				return default!;
			}
		}

		private string[] SafeGetInstances(PerformanceCounterCategory cat)
		{
			return SafePerf(() => cat.GetInstanceNames(), "GetInstanceNames")
				   ?? Array.Empty<string>();
		}

		private PerformanceCounter[] SafeGetCounters(PerformanceCounterCategory cat, string? instance = null)
		{
			return SafePerf(
				() => instance == null ? cat.GetCounters() : cat.GetCounters(instance),
				instance == null ? "GetCounters()" : $"GetCounters('{instance}')"
			) ?? Array.Empty<PerformanceCounter>();
		}

		internal async Task<List<string>> LoadCountersCoreAsync(string category, CancellationToken token)
		{
			if (string.IsNullOrEmpty(category))
				return new List<string>();

			//Log.Debug("[Pipeline] LoadCountersCoreAsync Reloading counters/instances — selection will reset to first item");

			var names = await RunOnBackgroundThread(() =>
			{
				var cat = SafePerf(() => new PerformanceCounterCategory(category), "new Category");
				if (cat == null)
					return Enumerable.Empty<string>();

				var insts = SafeGetInstances(cat);
				if (insts.Length == 0)
					return SafeGetCounters(cat).Select(c => c.CounterName);

				return insts
					.SelectMany(inst => SafeGetCounters(cat, inst).Select(c => c.CounterName))
					.Distinct();
			}, token);

			return names.OrderBy(x => x).ToList();
		}

		internal async Task<List<string>> LoadInstancesCoreAsync(string category, string counter, CancellationToken token)
		{
			if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(counter))
				return new List<string>();

			//Log.Debug("[Pipeline] LoadInstancesCoreAsync Reloading counters/instances — selection will reset to first item");

			var validInstances = await Task.Run(() =>
			{
				token.ThrowIfCancellationRequested();

				var cat = SafePerf(() => new PerformanceCounterCategory(category), "new Category");
				if (cat == null)
					return new List<string>();

				var insts = SafeGetInstances(cat);
				if (insts.Length == 0)
					return new List<string> { "" };

				return insts
					.Where(inst =>
					{
						token.ThrowIfCancellationRequested();
						return SafeGetCounters(cat, inst).Any(c => c.CounterName == counter);
					})
					.OrderBy(x => x)
					.ToList();
			}, token);

			return validInstances;
		}

		private void InitializeCommands()
		{
			// Editor-level actions
			AddCommand = new RelayCommand(
				async _ => await AddEditorToMetric(),
				_ => EditorPendingEdits
			);

			CancelCommand = new RelayCommand(       // Cancel all unsaved actions, Cancel button
				_ => CancelAllEdits(),
				_ => GlobalEditsPending
			);

			// Other actions
			CopyCommand = new RelayCommand(         // Copy Metric, (+) button
				_ => CopySelectedMetric(),
				_ => Selected != null
			);

			UpdateCommand = new RelayCommand(       // Update Metric from Editor, (<) button
				_ => UpdateEditorToMetric(),
				_ => EditorPendingEdits && IsEditingExistingMetric()
			);

			EditCommand = new RelayCommand(         // Copy Metric into Editor, (>) button
				async _ => await LoadSelectedIntoEditor(),
				_ => Selected != null
			);

			SaveCommand = new RelayCommand(			// Save all made changes, Save button
				_ => SaveAllPendingEdits(),
				_ => GlobalEditsPending && !IsLoading
			);

			RemoveCommand = new RelayCommand(       // Mark Metric for deletion (on save), (-) button
				_ => RemoveSelectedMetric(),
				_ => Selected != null
			);

			ResetCommand = new RelayCommand(
				async _ =>
				{
					if (ConfirmReset?.Invoke() ?? true)
						await ResetToDefaults();
				},
				_ => !IsAtDefaultConfiguration
			);

			CloseCommand = new RelayCommand(_ => CloseWindow());                        // Close config window (prompting if cnahges pending), Close button
			OpenDebugIconWindowCommand = new RelayCommand(_ => OpenDebugIconWindow());
			ShowMinMaxInfoCommand = new RelayCommand(_ => ShowMinMaxInfo());
		}

		private bool IsEditingExistingMetric()
		{
			//Log.Debug($"IsEditingExistingMetric: INSTANCE = {Editor.GetHashCode()}, Editor.Id = {Editor.Id}, Selected.Id = {Selected?.Id}");

			//Log.Debug($"IsEditingExistingMetric: (Selected != null) = {Selected != null}, "+
			//		  $"(Editor.Id != Guid.Empty) = {Editor.Id != Guid.Empty}, " +
			//		  $"(Selected.Id == Editor.Id) = {Selected.Id == Editor.Id}, " +
			//		  $"Result = {Selected != null && Editor.Id != Guid.Empty && Selected.Id == Editor.Id}!");
			return Selected != null &&
				   Editor.Id != Guid.Empty &&
				   Selected.Id == Editor.Id;
		}

		private async Task AddEditorToMetric()
		{
			if (Selected == null)
				return;

			Log.Debug($"AddMetric: Selected.GUID = {Selected.Id}, Editor.GUID = {Editor.Id}, Selected.DisplayName = {Selected.DisplayName}, Selected.ShowInTray = {Selected.ShowInTray}, Editor.ShowInTray = {Editor.ShowInTray}");

			// Create a new metric
			var settings = Editor.ToSettings();
			settings.Id = Guid.NewGuid();
			Log.Debug($"AddMetric: AFTER Editor.ToSettings()... setting.ShowInTray = {settings.ShowInTray}, Selected.ShowInTray = {Selected.ShowInTray}, Editor.ShowInTray = {Editor.ShowInTray}");

			//Log.Debug($"AddMetric: New counter... GUID ={settings.Id}");

			// Enforce tray icon limit immediately
			settings.ShowInTray = EnforceTrayLimit(null, settings.ShowInTray);

			var vm = new CounterViewModel(settings);
			vm.PropertyChanged += Counter_PropertyChanged;
			Metrics.Add(vm);

			Log.Debug($"AddMetric: BEFORE LoadSelectedIntoEditor... setting.ShowInTray = {settings.ShowInTray}");
			Editor.Id = settings.Id;
			Selected = vm;
			await LoadSelectedIntoEditor();
			Log.Debug($"AddMetric: After LoadSelectedIntoEditor... setting.ShowInTray = {settings.ShowInTray}, EditorPendingEdits  = {EditorPendingEdits}");

			OnPropertyChanged(nameof(TrayIconCount));
			OnPropertyChanged(nameof(TrayIconCountDisplay));

			ResetEditorDirtyState();
			//EditorPendingEdits = false;
			GlobalEditsPending = true;
			Log.Debug($"AddMetric: After LoadSelectedIntoEditor... EditorPendingEdits  = {EditorPendingEdits}");
		}

		private void UpdateEditorToMetric()
		{
			if (Selected == null)
				return;

			var target = Selected;

			//Log.Debug($"ApplyMetric: Selected.GUID = {Selected.Id}, Editor.GUID = {Editor.Id}, name = {Selected.DisplayName}");
			// This should always be true, or we would not be here, but does not hurt any!
			if (IsEditingExistingMetric())
			{
				// At this point, UpdateCommand's predicate guarantees:
				// - EditorPendingEdits == true
				// - Editor.Id != Guid.Empty
				// - Selected != null
				// - Selected.Id == Editor.Id

				var settings = Editor.ToSettings();

				//Log.Debug($"UpdateFromSettings: incoming.ShowInTray = {settings.ShowInTray}");
				//Log.Debug($"UpdateFromSettings: BEFORE, Selected.Settings.ShowInTray = {Selected.Settings.ShowInTray}");

				//settings.ShowInTray = EnforceTrayLimit(settings.ShowInTray);

				// Update the existing metric
				//Log.Debug($"AddMetric: Existing counter... updating");
				ApplyEditorToSelected(target, settings);

				//Log.Debug($"UpdateFromSettings: AFTER, Selected.Settings.ShowInTray = {Selected.Settings.ShowInTray}");

				OnPropertyChanged(nameof(TrayIconCount));
				OnPropertyChanged(nameof(TrayIconCountDisplay));
			}

			ResetEditorDirtyState();
			//EditorPendingEdits = false;
			GlobalEditsPending = true;
		}

		private void UpdateUiState()
		{
			OnPropertyChanged(nameof(MetricsCount));
			OnPropertyChanged(nameof(TrayIconCount));
			OnPropertyChanged(nameof(TrayIconCountDisplay));

			IsAtDefaultConfiguration = CheckIfDefault();
		}

		private void Counter_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(CounterViewModel.ShowInTray))
			{
				UpdateUiState();
			}
		}

		private async Task LoadSelectedIntoEditor()
		{
			if (Selected == null)
				return;

			BeginLoading();

			Log.Debug($"LoadSelectedIntoEditor: Before LoadFrom EditorPendingEdits = {EditorPendingEdits}");
			// Load the selected metric into the editor
			await Editor.LoadFrom(Selected);
			Log.Debug($"LoadSelectedIntoEditor: After LoadFrom EditorPendingEdits = {EditorPendingEdits}");

			// Show the metric is loaded
			//Log.Debug($"LoadSelectedIntoEditor: After LoadFrom, Id = {Selected.Id}, Category = {Selected.Category}, "+
			//	      $"Counter = {Selected.Counter}, Instance = {Selected.Instance}, "+
			//		  $"Editor counter = {Editor.SelectedCounter}, Editor.Instance = {Editor.SelectedInstance}");
			RefreshCommandStates();

			Log.Debug($"LoadSelectedIntoEditor: Before OnPropertyChanged EditorPendingEdits = {EditorPendingEdits}");
			OnPropertyChanged(nameof(Editor.SelectedCounter));
			OnPropertyChanged(nameof(Editor.SelectedInstance));
			Log.Debug($"LoadSelectedIntoEditor: After OnPropertyChanged EditorPendingEdits = {EditorPendingEdits}");

			if (Editor.ShowInTray)
			{
				LoadIconSetPreviewFrames(Editor.IconSet);
				UpdateDynamicPreview();
			}

			EndLoading();
			Editor.ResetDefaultInitializationGate();
			LoadSelectedCompleted?.Invoke();
			Log.Debug($"LoadSelectedIntoEditor: End EditorPendingEdits = {EditorPendingEdits}");
		}

		private void ApplyEditorToSelected(CounterViewModel target, CounterSettings settings)
		{
			if (target == null)
				return;

			//Log.Debug($"ApplyEditorToSelected: BEFORE update, target.Settings.ShowInTray = {target.Settings.ShowInTray}");
			//Log.Debug($"ApplyEditorToSelected: target.Id = {target.Id}, settings.ShowInTray = {settings.ShowInTray}");

			if (settings.Min == 0 && settings.Max == 0)
			{
				var defaults = new DefaultSettingsProvider().CreateDefaultCounter();
				settings.Min = defaults.Min;
				settings.Max = defaults.Max;
			}

			settings.ShowInTray = EnforceTrayLimit(target, settings.ShowInTray);

			target.UpdateFromSettings(settings);

			OnPropertyChanged(nameof(TrayIconCount));
			OnPropertyChanged(nameof(TrayIconCountDisplay));

			//Log.Debug($"ApplyEditorToSelected: ShowInTray = {settings.ShowInTray}");

			if (settings.ShowInTray)
			{
				LoadIconSetPreviewFrames(settings.IconSet);
				UpdateDynamicPreview();
			}

			Editor.ResetDefaultInitializationGate();
			EditorPendingEdits = false;

			//Log.Debug($"ApplyEditorToSelected: AFTER update, target.Settings.ShowInTray = {target.Settings.ShowInTray}");
		}

		private void CopySelectedMetric()
		{
			if (Selected == null)
				return;

			// Duplicate the selected metric's settings
			var settings = Selected.ToSettings();

			// Assign a new ID
			settings.Id = Guid.NewGuid();

			// Generate a new display name
			settings.DisplayName = GenerateCopyName(Selected.DisplayName);

			settings.ShowInTray = EnforceTrayLimit(null, settings.ShowInTray);

			// Create a new VM from the copied settings
			var vm = new CounterViewModel(settings);

			// Wire up property changed
			vm.PropertyChanged += Counter_PropertyChanged;

			// Add it to the list
			Metrics.Add(vm);

			// Select it (this loads it into the editor automatically)
			Selected = vm;

			OnPropertyChanged(nameof(TrayIconCount));
			OnPropertyChanged(nameof(TrayIconCountDisplay));

			// Mark global edits pending
			GlobalEditsPending = true;
			UpdateUiState();
		}

		private void RemoveSelectedMetric()
		{
			if (Selected == null)
				return;

			var toRemove = Selected;
			int idx = Metrics.IndexOf(toRemove);

			_pendingRemoval = Selected;
			_pendingRemoval.IsPendingRemoval = true;
			//EditorPendingEdits = true;
			//Metrics.Remove(toRemove);

			if (Selected.ShowInTray)
			{
				// The metric is *intended* to be removed, so the tray icon count drops
				OnPropertyChanged(nameof(TrayIconCount));
				OnPropertyChanged(nameof(TrayIconCountDisplay));

				// And the tray manager should hide the icon immediately
				OnMetricPendingRemoval?.Invoke(_pendingRemoval);
			}
			if (Metrics.Any())
			{
				// Select the next logical item
				Selected = Metrics[Math.Min(idx, Metrics.Count - 1)];
				//EditorPendingEdits = true;
			}
			else
			{
				// No metrics left → reset editor
				Selected = null;
				Editor.LoadDefaults();
				EditorPendingEdits = false;
			}

			//Log.Debug($"RemoveSelected: TrayIconCount = {TrayIconCount}");
			GlobalEditsPending = true;
			UpdateUiState();
		}

		private string GenerateCopyName(string originalName)
		{
			// First try: "Copy of X"
			string baseName = $"Copy of {originalName}";
			if (!Metrics.Any(c => c.DisplayName == baseName))
				return baseName;

			// If that exists, try "Copy (2) of X", "Copy (3) of X", etc.
			int i = 2;
			while (true)
			{
				string candidate = $"Copy ({i}) of {originalName}";
				if (!Metrics.Any(c => c.DisplayName == candidate))
					return candidate;

				i++;
			}
		}

		private void CancelAllEdits()
		{
			if (!(ConfirmCancel?.Invoke() ?? true))
				return;

			// Restore disk state
			LoadSettingsFromDisk();

			// Snapshot now matches disk again
			SaveSnapshot();

			IsAtDefaultConfiguration = CheckIfDefault();

			// ⭐ Refresh UI
			if (Selected != null)
				LoadSelectedIntoEditor();   // reloads editor, preview, command states

			//StopPreviewTimer();
			CancelAllWork();

			//StopPreviewTimer();
			CancelAllWork();

			OnPropertyChanged(nameof(TrayIconCount));
			OnPropertyChanged(nameof(TrayIconCountDisplay));

			RefreshCommandStates();

			GlobalEditsPending = false;
			ResetEditorDirtyState();
			//EditorPendingEdits = false;
		}

		private bool CheckIfDefault()
		{
			var defaults = SettingsOptions.CreateDefault();

			var currentDto = SettingsMapper.ToDto(_globalSettings);
			var defaultDto = SettingsMapper.ToDto(defaults);

			if (currentDto.Metrics.Count != defaultDto.Metrics.Count)
				return false;

			for (int i = 0; i < currentDto.Metrics.Count; i++)
			{
				if (!currentDto.Metrics[i].IsEquivalentToDefault(defaultDto.Metrics[i]))
					return false;
			}

			return true;
		}

		private void SaveAllPendingEdits()
		{
			if (_pendingRemoval != null)
			{
				if (_pendingRemoval?.ShowInTray == true)
				{
					// Restore the intended tray icon count
					OnPropertyChanged(nameof(TrayIconCount));
					OnPropertyChanged(nameof(TrayIconCountDisplay));

					// Restore the icon
					OnMetricPendingRemoval?.Invoke(_pendingRemoval);
				}

				Metrics.Remove(_pendingRemoval);
				_pendingRemoval.IsPendingRemoval = false;
				_pendingRemoval = null;
			}

			// Apply editor changes if needed
			//if (Selected != null && EditorPendingEdits)
			//	Selected.UpdateFromSettings(Editor.ToSettings());

			// Snapshot BEFORE writing to disk (No more Cancel)!
			SaveSnapshot();

			// Build a fresh SettingsOptions from the ViewModel
			var newSettings = SettingsMapper.FromViewModel(this);

			// Convert to DTO and enqueue async save
			var dto = SettingsMapper.ToDto(newSettings);
			SettingsSaveQueue.Enqueue(dto);

			// Replace runtime settings with the NEW settings
			_main.ReplaceSettings(newSettings);

			// Clear dirty flags
			GlobalEditsPending = false;
			//EditorPendingEdits = false;

			// Recompute default-state flag
			IsAtDefaultConfiguration = CheckIfDefault();
		}

		/* No longer used, Cancel does this!
		private void DiscardEdits()
		{
			if (Selected == null)
			{
				return;
			}

			if (_pendingRemoval != null)
			{
				_pendingRemoval.IsPendingRemoval = false;
				_pendingRemoval = null;
			}

			// ------------------------------------------------------------
			// Reload the editor directly from the selected metric
			// (no selection clearing, no resetting, no pipeline)
			// ------------------------------------------------------------
			Editor.LoadFrom(Selected);

			// ------------------------------------------------------------
			// Editor is now clean
			// ------------------------------------------------------------
			EditorPendingEdits = false;

			// ------------------------------------------------------------
			// Update UI
			// ------------------------------------------------------------
			IsAtDefaultConfiguration = CheckIfDefault();
			RefreshCommandStates();
		}
		*/

		private void LoadSettingsFromDisk()
		{
			var dto = SettingsRepository.Load();
			var settings = SettingsMapper.ToOptions(dto);   // <-- convert to runtime model

			_main.ReplaceSettings(settings);
			LoadIntoViewModel(settings);

			SaveSnapshot();
		}

		private void LoadIntoViewModel(SettingsOptions settings)
		{
			// ------------------------------------------------------------
			// Replace underlying settings
			// ------------------------------------------------------------
			_globalSettings = settings;
			GlobalSettings = settings;

			// ------------------------------------------------------------
			// Rebuild metric list
			// ------------------------------------------------------------
			Metrics.Clear();
			foreach (var settingsItem in settings.Metrics)
			{
				Metrics.Add(new CounterViewModel(settingsItem));
			}

			// ------------------------------------------------------------
			// Select the first metric (or clear editor)
			// ------------------------------------------------------------
			_selected = null;
			OnPropertyChanged(nameof(Selected));

			Selected = Metrics.FirstOrDefault();

			// ------------------------------------------------------------
			// Reset dirty flags (this is ALWAYS a full reload)
			// ------------------------------------------------------------
			GlobalEditsPending = false;
			EditorPendingEdits = false;

			// ------------------------------------------------------------
			// Recompute default-state flag
			// ------------------------------------------------------------
			IsAtDefaultConfiguration = CheckIfDefault();

			RefreshCommandStates();
		}

		void CloseWindow()
		{
			RequestWindowClose();
		}

		private void RequestWindowClose()
		{
			RequestClose?.Invoke();
			_previewTimer?.Stop();
		}

		public void StartPreviewTimer()
		{
			if (_previewTimer != null)
				return; // prevent double-start

			_previewTimer = new DispatcherTimer
			{
				Interval = TimeSpan.FromSeconds(TrayIconConfig.IconSetPreviewTimer)
			};

			_previewTimer.Tick += (_, __) => UpdateDynamicPreview();
			_previewTimer.Start();
		}

		public void StopPreviewTimer()
		{
			_previewTimer?.Stop();
		}

		private void RefreshCommandStates()
		{
			(AddCommand as RelayCommand)?.RaiseCanExecuteChanged();
			(UpdateCommand as RelayCommand)?.RaiseCanExecuteChanged();
			(EditCommand as RelayCommand)?.RaiseCanExecuteChanged();
			(CopyCommand as RelayCommand)?.RaiseCanExecuteChanged();
			(RemoveCommand as RelayCommand)?.RaiseCanExecuteChanged();
			(ResetCommand as RelayCommand)?.RaiseCanExecuteChanged();
			(SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
			(CancelCommand as RelayCommand)?.RaiseCanExecuteChanged();
			(CloseCommand as RelayCommand)?.RaiseCanExecuteChanged();
		}

		private async Task ResetToDefaults()
		{
			BeginLoading();

			try
			{
				var defaults = SettingsOptions.CreateDefault();

				// Clear selection BEFORE clearing the list
				Selected = null;

				// Replace the entire metrics list
				Metrics.Clear();
				foreach (var m in defaults.Metrics)
					Metrics.Add(new CounterViewModel(m));

				// Select the first metric
				Selected = Metrics.FirstOrDefault();

				// Reset shadow to avoid stale selections
				_shadow.Reset();

				// Load editor
				if (Selected != null)
					await Editor.LoadFrom(Selected);

				GlobalEditsPending = true;
				EditorPendingEdits = false;
				IsAtDefaultConfiguration = true;
			}
			finally
			{
				EndLoading();
				OnPropertyChanged(nameof(TrayIconCount));
				OnPropertyChanged(nameof(TrayIconCountDisplay));
			}
		}

		public void RestoreMetrics(IEnumerable<CounterSettingsDto> snapshot)
		{
			Metrics.Clear();

			foreach (var dto in snapshot)
				Metrics.Add(new CounterViewModel(dto.ToSettings()));

			Selected = Metrics.FirstOrDefault();

			if (Selected != null)
				_ = Editor.LoadFrom(Selected);
		}

		public void SaveSnapshot() => SaveSnapshotCore();
		public void SaveSnapshotCore()
		{
			_lastSavedMetricsSnapshot = GlobalSettings.Metrics
				.Select(SettingsMapper.ToCounterDto)
				.ToList();
		}

		private void LoadIconSetPreviewFrames(string? iconSetName)
		{
			if (string.IsNullOrEmpty(iconSetName))
			{
				IconSetPreviewFrames = null;
				return;
			}

			if (!IconSetConfig.IconSets.TryGetValue(iconSetName, out var set))
			{
				IconSetPreviewFrames = null;
				return;
			}

			var frames = new List<BitmapSource>();

			foreach (var uri in set.Frames)
			{
				try
				{
					using var stream = IconLoader.TryOpenStream(set, uri);
					if (stream == null)
						continue;

					var bmp = new BitmapImage();
					bmp.BeginInit();
					bmp.StreamSource = stream;
					bmp.CacheOption = BitmapCacheOption.OnLoad;
					bmp.EndInit();

					frames.Add(bmp);
				}
				catch
				{
					// Ignore individual frame failures
				}
			}

			IconSetPreviewFrames = frames.ToArray();
		}

		internal void UpdateDynamicPreview()
		{
			// Generate a random metric value
			int min = (int)Math.Floor(Editor.Min);
			int max = (int)Math.Ceiling(Editor.Max);
			if (max < min)
				max = min;
			double value = _random.Next(min, max + 1);

			// Text mode
			if (Editor.UseTextTrayIcon)
			{
				TrayPreviewImage = TrayIconGenerator.CreateTextBitmapSource(
					((int)value).ToString(),
					Editor.TrayAccentColor.ToDrawingColor(),
					Editor.AutoTrayBackground
						? UIColors.GetTrayBackground(Editor.TrayAccentColor.ToDrawingColor(),
							autoContrast: true)
						: Editor.TrayBackgroundColor.ToDrawingColor(),
					dpiScale: 1.5
				);
				return;
			}

			// Icon-set mode
			int frameIndex = TrayIconGenerator.GetFrameIndex(
				value,
				Editor.Min,
				Editor.Max,
				IconSetPreviewFrames?.Length ?? 0
			);

			// Save guard against any unusual/unexpected values returned.
			if (IconSetPreviewFrames == null || IconSetPreviewFrames.Length == 0)
				return;
			if (frameIndex < 0 || frameIndex >= IconSetPreviewFrames.Length)
				return;

			TrayPreviewImage = IconSetPreviewFrames[frameIndex];
		}

		private void OpenDebugIconWindow()
		{
			if (PerformanceTrayMonitor.Views.DebugIconWindow.IsOpen)
				return;

			var win = new PerformanceTrayMonitor.Views.DebugIconWindow(Editor.IconSet)
			{
				Owner = System.Windows.Application.Current.MainWindow
			};

			win.Show();
		}

		private Task<T> RunOnBackgroundThread<T>(Func<T> func, CancellationToken token)
		{
			var tcs = new TaskCompletionSource<T>();

			Thread thread = new Thread(() =>
			{
				try
				{
					if (token.IsCancellationRequested)
					{
						tcs.TrySetCanceled(token);
						return;
					}

					var result = func();
					tcs.TrySetResult(result);
				}
				catch (Exception ex)
				{
					tcs.TrySetException(ex);
				}
			});

			thread.IsBackground = true;
			thread.SetApartmentState(ApartmentState.MTA);
			thread.Start();

			return tcs.Task;
		}

		private void ShowMinMaxInfo()
		{
			MessageBox.Show(TrayIconConfig.MinMaxInformationText,
				TrayIconConfig.MinMaxInformationHeader,
				MessageBoxButton.OK,
				MessageBoxImage.Information);
		}

		public void NotifyTraySettingsChanged()
		{
			OnPropertyChanged(nameof(CanShowInTray));   // Make sure UI Enables/Disables Show in tray correctly.
		}
	}
}
