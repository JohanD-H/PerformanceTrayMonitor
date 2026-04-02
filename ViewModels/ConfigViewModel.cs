using PerformanceTrayMonitor.Common;
using PerformanceTrayMonitor.Configuration;
using PerformanceTrayMonitor.Extensions;
using PerformanceTrayMonitor.Models;
using PerformanceTrayMonitor.Settings;
using PerformanceTrayMonitor.Tray;
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

		// ============================================================
		//  CLEAN STATE MODEL
		// ============================================================

		/// <summary>
		/// True when the configuration as a whole has unsaved changes.
		/// This drives Save/Cancel.
		/// </summary>
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

		/// <summary>
		/// True when the editor UI has unsaved edits for the currently selected metric.
		/// This drives Apply/Discard.
		/// </summary>
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
			EditorPendingEdits = true;
		}

		/// <summary>
		/// True when the editor UI has unsaved changes or edits for the currently selected metric.
		/// This drives Details in the UI.
		/// </summary>
		public bool AnyPendingEdits => EditorPendingEdits || GlobalEditsPending;

		/// <summary>
		/// True when the entire configuration matches the default template.
		/// Drives the Reset button.
		// </summary>
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

		/// <summary>
		/// Suppresses SelectedInstance changes during ItemsSource rebuilds.
		/// </summary>
		//internal bool _suppressAutoSelect;

		/// <summary>
		/// True only during a full configuration reset (ResetToDefaults).
		/// NOT used for Discard or selection reloads.
		/// </summary>
		//private bool _isResettingConfig;

		/// <summary>
		/// UI loading shield (unchanged).
		/// </summary>
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
		public SettingsOptions GlobalSettings
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

		private bool _isSelectionLoadInProgress;
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

		public int TrayIconCount => Metrics.Count(c => c.ShowInTray);
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
		public ICommand? ApplyCommand { get; private set; }
		public ICommand? CopyCommand { get; private set; }
		public ICommand? CancelCommand { get; private set; }
		public ICommand? DiscardCommand { get; private set; }
		public ICommand? OpenDebugIconWindowCommand { get; private set; }
		public ICommand? RemoveCommand { get; private set; }
		public ICommand? ResetCommand { get; private set; }
		public ICommand? SaveCommand { get; private set; }
		public ICommand? CloseCommand { get; private set; }
		public ICommand? ShowMinMaxInfoCommand { get; private set;  }

		//private readonly bool _useTextTrayIcon;
		//private System.Windows.Media.Color _trayAccentColor;
		//private bool _autoTrayBackground;
		//private System.Windows.Media.Color _trayBackgroundColor;
		public ObservableCollection<CounterViewModel> Metrics { get; }
			= new ObservableCollection<CounterViewModel>();
		public int MetricsCount => Metrics.Count;
		private List<CounterSettingsDto> _lastSavedMetricsSnapshot = new();
		public IReadOnlyList<CounterSettingsDto> LastSavedMetricsSnapshot
			=> _lastSavedMetricsSnapshot;

		internal readonly ShadowMetricState _shadow = new ShadowMetricState();

		public BitmapSource[]? IconSetPreviewFrames { get; set; }
		//public int CurrentFrameIndex => 0;
		public Window? OwnerWindow { get; set; }

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
				vm.PropertyChanged += Counter_PropertyChanged;   // ⭐ add this
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
			set => _ = SetSelectedAsync(value);
		}

		public async Task SetSelectedAsync(CounterViewModel? value)
		{
			if (_selected == value)
				return;

			_selected = value;

			if (_selected != null)
			{
				// Run the full shadow → load → commit pipeline
				await ApplySelectedAsync(_selected);
			}

			// Notify UI that the *selected metric* changed
			OnPropertyChanged(nameof(Selected));

			RefreshCommandStates();
		}

		public sealed class ShadowMetricState
		{
			public string Category = "";
			public string Counter = "";
			public string Instance = "";

			public List<string> CountersInCategory { get; } = new();
			public List<string> Instances { get; } = new();
		}

		internal bool _isCommittingShadow;

		private void CommitShadowToEditor()
		{
			_isCommittingShadow = true;
			Editor._suppressEditorSetters = true;

			try
			{
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
			if (IsLoading || IsSelectionLoadInProgress)
			{
				return;
			}

			// ------------------------------------------------------------
			// Real user edits → mark editor dirty
			// ------------------------------------------------------------
			EditorPendingEdits = true;
			IsAtDefaultConfiguration = false;

			// ------------------------------------------------------------
			// Handle special cases
			// ------------------------------------------------------------
			switch (e.PropertyName)
			{
				case nameof(Editor.SelectedCategory):
					// Identity changed → force new metric
					Editor.Id = Guid.Empty;
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
			UpdateDynamicPreview();
			OnPropertyChanged(nameof(MetricsCount));
			OnPropertyChanged(nameof(StatusText));
		}

		public async Task ApplySelectedFromEditorAsync()
		{
			if (IsSelectionLoadInProgress)
				return;

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
					CountersInCategory = new ObservableCollection<string>(_shadow.CountersInCategory);
					OnPropertyChanged(nameof(CountersInCategory));

					Instances.Clear();
					foreach (var inst in _shadow.Instances)
						Instances.Add(inst);
					OnPropertyChanged(nameof(Instances));
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

				LoadIconSetPreviewFrames(Editor.IconSet);
				UpdateDynamicPreview();
			}
		}

		public async Task ApplySelectedAsync(CounterViewModel vm)
		{
			if (IsSelectionLoadInProgress)
				return;

			IsSelectionLoadInProgress = true;
			BeginLoading();

			try
			{
				// Load basic data into shadow
				Editor.LoadFrom(vm);

				// Load counters/instances into shadow (background)
				await Task.Run(async () =>
				{
					var counters = await LoadCountersCoreAsync(_shadow.Category, _cts.Token);
					var instances = await LoadInstancesCoreAsync(_shadow.Category, _shadow.Counter, _cts.Token);

					_shadow.CountersInCategory.Clear();
					_shadow.CountersInCategory.AddRange(counters);

					_shadow.Instances.Clear();
					_shadow.Instances.AddRange(instances);
				});

				// Push lists to UI
				await Application.Current.Dispatcher.InvokeAsync(() =>
				{
					CountersInCategory = new ObservableCollection<string>(_shadow.CountersInCategory);
					OnPropertyChanged(nameof(CountersInCategory));

					Instances.Clear();
					foreach (var inst in _shadow.Instances)
						Instances.Add(inst);
					OnPropertyChanged(nameof(Instances));
				});

				// Auto-select logic
				// Counter
				//string _t1 = string.IsNullOrEmpty(vm.Counter) ? "Null" : vm.Counter;
				//Log.Debug($"ApplySelectedAsync: Counter = {_t1}");
				if (string.IsNullOrWhiteSpace(vm.Counter) ||
					!_shadow.CountersInCategory.Contains(vm.Counter))
				{
					if (_shadow.CountersInCategory.Count > 0)
						_shadow.Counter = _shadow.CountersInCategory[0];
				}
				else
				{
					_shadow.Counter = vm.Counter;
				}
				//Log.Debug($"ApplySelectedAsync: _shadow.Counter = {_shadow.Counter}");

				// Auto-select logic
				// Instance
				//string _t2 = string.IsNullOrEmpty(vm.Instance) ? "Null" : vm.Instance;
				//Log.Debug($"ApplySelectedAsync: Instance = {_t2}");
				if (string.IsNullOrWhiteSpace(vm.Instance) ||
					!_shadow.Instances.Contains(vm.Instance))
				{
					if (_shadow.Instances.Count > 0)
						_shadow.Instance = _shadow.Instances[0];
				}
				else
				{
					_shadow.Instance = vm.Instance;
				}
				//Log.Debug($"ApplySelectedAsync: _shadow.Instance = {_shadow.Instance}");

				// Commit shadow → UI
				CommitShadowToEditor();
			}
			finally
			{
				EndLoading();
				IsSelectionLoadInProgress = false;

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

		public async Task LoadCountersForCategoryAsync(string category, CancellationToken token)
		{
			try
			{
				token.ThrowIfCancellationRequested();

				if (string.IsNullOrEmpty(category))
					return;

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

				CountersInCategory = new ObservableCollection<string>(names.OrderBy(x => x));
				OnPropertyChanged(nameof(CountersInCategory));
			}
			catch (OperationCanceledException)
			{
				// Nothing
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Unexpected error in LoadCountersForCategoryAsync");
			}
		}

		internal async Task<List<string>> LoadCountersCoreAsync(string category, CancellationToken token)
		{
			if (string.IsNullOrEmpty(category))
				return new List<string>();

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

		public async Task LoadInstancesForCounterAsync(string category, string counter, CancellationToken token)
		{
			try
			{
				token.ThrowIfCancellationRequested();

				if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(counter))
					return;

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

				await Application.Current.Dispatcher.InvokeAsync(() =>
				{
					Instances.Clear();
					foreach (var inst in validInstances)
					{
						Instances.Add(inst);
					}

					OnPropertyChanged(nameof(Instances));
				});
			}
			catch (OperationCanceledException)
			{
				// Nothing
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Unexpected error in LoadInstancesForCounterAsync");
			}
		}

		private void InitializeCommands()
		{
			// Editor-level actions (Apply / Discard)
			ApplyCommand = new RelayCommand(
				_ => ApplyCounter(),
				_ => EditorPendingEdits
			);

			DiscardCommand = new RelayCommand(
				_ => DiscardEdits(),
				_ => EditorPendingEdits
			);

			// Global-level actions (Save / Cancel)
			SaveCommand = new RelayCommand(
				_ => Save(),
				_ => GlobalEditsPending
			);

			CancelCommand = new RelayCommand(
				_ => CancelEdits(),
				_ => GlobalEditsPending
			);

			// Other actions
			CopyCommand = new RelayCommand(
				_ => CopySelectedMetric(),
				_ => Selected != null
			);

			RemoveCommand = new RelayCommand(
				_ => RemoveSelected(),
				_ => Selected != null
			);

			ResetCommand = new RelayCommand(
				_ => { if (ConfirmReset?.Invoke() ?? true) ResetToDefaults(); },
				_ => !IsAtDefaultConfiguration
			);

			CloseCommand = new RelayCommand(_ => CloseWindow());
			OpenDebugIconWindowCommand = new RelayCommand(_ => OpenDebugIconWindow());
			ShowMinMaxInfoCommand = new RelayCommand(_ => ShowMinMaxInfo());
		}

		private bool IsEditingExistingMetric()
		{
			return Selected != null &&
				   Editor.Id != Guid.Empty &&
				   Selected.Id == Editor.Id;
		}

		private void ApplyCounter()
		{
			if (Selected == null)
				return;

			//Log.Debug($"ApplyCounter: Selected.GUID = {Selected.Id}, Editor.GUID = {Editor.Id}, name = {Selected.DisplayName}");
			if (IsEditingExistingMetric())
			{
				// Update the existing metric
				//Log.Debug($"ApplyCounter: Existing counter... updsating");
				ApplyEditorToSelected();
			}
			else
			{
				// Create a new metric
				//Log.Debug($"ApplyCounter: New counter... adding");
				var settings = Editor.ToSettings();
				settings.Id = Guid.NewGuid();

				var vm = new CounterViewModel(settings);
				vm.PropertyChanged += Counter_PropertyChanged;
				Metrics.Add(vm);

				Selected = vm;
				EditorPendingEdits = false;
			}

			GlobalEditsPending = true;

			UpdateUiState();
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

		private void ApplyEditorToSelected()
		{
			if (Selected == null)
				return;

			var settings = Editor.ToSettings();

			Selected.UpdateFromSettings(settings);

			// Editor is now clean
			EditorPendingEdits = false;
		}

		private void CopySelectedMetric()
		{
			if (Selected == null)
				return;

			// Load the selected metric into the editor
			Editor.LoadFrom(Selected);

			// Prepare as a new metric
			Editor.Id = Guid.NewGuid();
			Editor.DisplayName = GenerateCopyName(Selected.DisplayName);

			// Editor now has unsaved edits
			EditorPendingEdits = true;
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

		private void CancelEdits()
		{
			if (!(ConfirmCancel?.Invoke() ?? true))
				return;

			// Restore disk state
			LoadSettingsFromDisk();

			// Snapshot now matches disk again
			SaveSnapshot();

			GlobalEditsPending = false;
			EditorPendingEdits = false;

			IsAtDefaultConfiguration = CheckIfDefault();

			RequestWindowClose();
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

		private void RemoveSelected()
		{
			if (Selected == null)
				return;

			var toRemove = Selected;
			int idx = Metrics.IndexOf(toRemove);

			Metrics.Remove(toRemove);

			if (Metrics.Any())
			{
				// Select the next logical item
				Selected = Metrics[Math.Min(idx, Metrics.Count - 1)];
				EditorPendingEdits = true;
			}
			else
			{
				// No metrics left → reset editor
				Selected = null;
				Editor.LoadDefaults();
				EditorPendingEdits = false;
			}

			GlobalEditsPending = true;
			UpdateUiState();
		}

		private void Save()
		{
			// Apply editor changes if needed
			if (Selected != null && EditorPendingEdits)
				Selected.UpdateFromSettings(Editor.ToSettings());

			// Snapshot BEFORE writing to disk (for Cancel)
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
			EditorPendingEdits = false;

			// Recompute default-state flag
			IsAtDefaultConfiguration = CheckIfDefault();
		}

		private void DiscardEdits()
		{
			if (Selected == null)
			{
				return;
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
			(ApplyCommand as RelayCommand)?.RaiseCanExecuteChanged();
			(DiscardCommand as RelayCommand)?.RaiseCanExecuteChanged();
			(CopyCommand as RelayCommand)?.RaiseCanExecuteChanged();
			(RemoveCommand as RelayCommand)?.RaiseCanExecuteChanged();
			(ResetCommand as RelayCommand)?.RaiseCanExecuteChanged();
			(SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
			(CancelCommand as RelayCommand)?.RaiseCanExecuteChanged();
			(CloseCommand as RelayCommand)?.RaiseCanExecuteChanged();
		}

		private void ResetToDefaults()
		{
			BeginLoading();
			//_isResettingConfig = true;

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

				// Load editor
				if (Selected != null)
					Editor.LoadFrom(Selected);

				GlobalEditsPending = true;
				EditorPendingEdits = false;
				IsAtDefaultConfiguration = true;

			}
			finally
			{
				//_isResettingConfig = false;
				EndLoading();
			}
		}

		/*
		private static List<CounterSettingsDto> CloneMetrics(IEnumerable<CounterSettingsDto> source)
		{
			var clone = new List<CounterSettingsDto>();

			foreach (var m in source)
			{
				clone.Add(new CounterSettingsDto
				{
					Id = m.Id,
					Category = m.Category,
					Counter = m.Counter,
					Instance = m.Instance,
					DisplayName = m.DisplayName,
					Min = m.Min,
					Max = m.Max,
					ShowInTray = m.ShowInTray,
					IconSet = m.IconSet,
					UseTextTrayIcon = m.UseTextTrayIcon,
					TrayAccentColorArgb = m.TrayAccentColorArgb,
					AutoTrayBackground = m.AutoTrayBackground,
					TrayBackgroundColorArgb = m.TrayBackgroundColorArgb
				});
			}

			return clone;
		}
		*/

		public void RestoreMetrics(IEnumerable<CounterSettingsDto> snapshot)
		{
			Metrics.Clear();

			foreach (var dto in snapshot)
				Metrics.Add(new CounterViewModel(dto.ToSettings()));

			Selected = Metrics.FirstOrDefault();

			if (Selected != null)
				Editor.LoadFrom(Selected);
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

		private static string FormatValueForTray(double value)
		{
			return $"{(int)value}%";
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
			MessageBox.Show(
				"How Min and Max are used:\n\n" +
				"Line graph:\n" +
				"The small graph uses Min and Max to scale the line. " +
				"Values below Min appear at the bottom, values above Max at the top.\n\n" +
				"Animated icon (iconset):\n" +
				"If you use an animated icon, Min and Max determine which frame is shown. " +
				"The current value is mapped between Min and Max to pick the correct animation frame.",
				"Min / Max Information",
				MessageBoxButton.OK,
				MessageBoxImage.Information);
		}

		public static void DoEvents()
		{
			// Forces a Layout, Measure, Arrange, Render. Input, Anything queued (up to Render priority) to flush!
			var frame = new DispatcherFrame();
			Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Render, new DispatcherOperationCallback(delegate (object parameter)
			{
				frame.Continue = false;
				return null;
			}), null);
			Dispatcher.PushFrame(frame);
		}

		public static async Task AwaitRenderAsync()
		{
			await Task.Yield();   // let dispatcher run queued work
			await Task.Delay(100);  // allow a render frame
		}
	}
}
