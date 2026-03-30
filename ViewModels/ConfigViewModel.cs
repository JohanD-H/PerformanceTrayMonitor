using PerformanceTrayMonitor.Common;
using PerformanceTrayMonitor.Configuration;
using PerformanceTrayMonitor.Extensions;
using PerformanceTrayMonitor.Models;
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
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

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
				RefreshCommandStates();
			}
		}
		private bool _editorPendingEdits;

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
		private bool _pendingPreviewUpdate;

		// ============================================================
		//  INTERNAL SHIELDS (minimal, intentional)
		// ============================================================

		/// <summary>
		/// Suppresses editor PropertyChanged noise during LoadFrom, selection changes, etc.
		/// Does NOT affect global state.
		/// </summary>
		//internal bool SuppressEditorChanges;

		/// <summary>
		/// Suppresses SelectedInstance changes during ItemsSource rebuilds.
		/// </summary>
		//internal bool SuppressInstanceChange;

		/// <summary>
		/// Suppresses SelectedInstance changes during ItemsSource rebuilds.
		/// </summary>
		internal bool _suppressAutoSelect;

		/// <summary>
		/// True only during a full configuration reset (ResetToDefaults).
		/// NOT used for Discard or selection reloads.
		/// </summary>
		private bool _isResettingConfig;

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
			}
		}

		private int _loadingCount;

		private void BeginLoading()
		{
			_loadingCount++;
			IsLoading = true;
			OnPropertyChanged(nameof(StatusText));
		}

		private void EndLoading()
		{
			_loadingCount--;
			if (_loadingCount <= 0)
			{
				_loadingCount = 0;
				IsLoading = false;
			}

			OnPropertyChanged(nameof(StatusText));
			RefreshCommandStates();
		}

		// ============================================================
		//  SETTINGS + COLLECTIONS
		// ============================================================

		private SettingsOptions _globalSettings;
		public SettingsOptions GlobalSettings
		{
			get => _globalSettings;
			set { _globalSettings = value; OnPropertyChanged(); }
		}

		public ObservableCollection<CounterViewModel> Counters { get; } = new();
		public ObservableCollection<string> Categories { get; } = new();
		public ObservableCollection<string> CountersInCategory { get; private set; } = new();
		public ObservableCollection<string> Instances { get; private set; } = new();

		public ObservableCollection<string> AvailableIconSets { get; } = new(IconSetConfig.IconSets.Keys.OrderBy(x => x));

		public CounterEditorViewModel Editor { get; }

		private bool _isSelectionLoadInProgress = false;
		internal bool IsSelectionLoadInProgress => _isSelectionLoadInProgress;

		/*
		// The following two gates are used to indicate what is happening in the configuration
		// 1 - GlobalEditsPending -> true = Changes are Applied, but NOT Saved/Canceled (with warning)! false = No changed in the configuration (maybe default) to Save/Canncel
		// 2 - EditorPendingEdits -> true = There are changes in the Editor, but these changes are not Applied/Discarded, i.e.made global! false = no edits made
		public bool GlobalEditsPending { get; private set; }
		public bool EditorPendingEdits { get; private set; }

		// ******* The below needs cleaning for sure!!!
		internal bool SuppressInstanceChange;
		internal bool SuppressEditorChanges;
		private bool _isResetting;

		private bool _hasPendingEdits;
		public bool HasAppliedChanges
		{
			get => _hasAppliedChanges;
			set { _hasAppliedChanges = value; OnPropertyChanged(); }
		}
		private bool _hasAppliedChanges;

		*/
		public Action? RequestClose { get; set; }
		private DispatcherTimer _previewTimer;
		private readonly Random _random = new();
		public Func<bool>? ConfirmReset { get; set; }
		public Func<bool>? ConfirmCancel { get; set; }
		public Func<bool>? ConfirmClose { get; set; }

		public CancellationTokenSource _cts = new();
		public CancellationTokenSource _instanceLoadCts;
		public void CancelAllWork()
		{
			_cts.Cancel();
		}

		public int TrayIconCount => Counters.Count(c => c.ShowInTray);
		public string TrayIconCountDisplay =>
			$"Tray icons: {TrayIconCount}/{TrayIconConfig.MaxCounterTrayIcons}";

		private BitmapSource _trayPreviewImage;
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
		public ICommand ApplyCommand { get; private set; }
		public ICommand CopyCommand { get; private set; }
		public ICommand CancelCommand { get; private set; }
		public ICommand DiscardCommand { get; private set; }
		public ICommand OpenDebugIconWindowCommand { get; private set; }
		public ICommand RemoveCommand { get; private set; }
		public ICommand ResetCommand { get; private set; }
		public ICommand SaveCommand { get; private set; }
		public ICommand CloseCommand { get; private set; }
		public ICommand ShowMinMaxInfoCommand { get; private set;  }

		private bool _useTextTrayIcon;
		private System.Windows.Media.Color _trayAccentColor;
		private bool _autoTrayBackground;
		private System.Windows.Media.Color _trayBackgroundColor;
		public ObservableCollection<CounterViewModel> Metrics { get; }
			= new ObservableCollection<CounterViewModel>();
		public int MetricsCount => Metrics.Count;
		private List<CounterSettingsDto> _lastSavedMetricsSnapshot = new();
		public IReadOnlyList<CounterSettingsDto> LastSavedMetricsSnapshot
			=> _lastSavedMetricsSnapshot;

		public BitmapSource[]? IconSetPreviewFrames { get; set; }
		public int CurrentFrameIndex => 0;
		public Window OwnerWindow { get; set; }

		public ConfigViewModel(SettingsOptions settings, MainViewModel main)
		{
			GlobalSettings = settings;
			_main = main;
			Editor = new CounterEditorViewModel(this);

			Log.Debug($"Editor instance hash = {Editor.GetHashCode()}");

			var cats = PerformanceCounterCategory
				.GetCategories()
				.Select(c => c.CategoryName)
				.OrderBy(x => x);

			foreach (var cat in cats)
				Categories.Add(cat);

			// Load existing metrics from GlobalSettings
			foreach (var metricDto in GlobalSettings.Metrics)
			{
				Log.Debug($"Metrics item type = {metricDto?.GetType().FullName ?? "NULL"}");
				Metrics.Add(new CounterViewModel(metricDto));
			}

			InitializeCommands();

			if (Metrics.Any())
				Selected = Metrics.First();
			else
				Editor.LoadDefaults();

			Editor.PropertyChanged += Editor_PropertyChanged;

			// If LoadIntoViewModel just re-applies settings, pass GlobalSettings, not the ctor parameter
			LoadIntoViewModel(GlobalSettings);

			// Initial snapshot
			SaveSnapshot();
		}
		/*
		public ConfigViewModel(SettingsOptions settings, MainViewModel main)
		{
			// settings (parameter) goes into GlobalSettings (property).
			this.GlobalSettings = settings;

			_main = main;
			Editor = new CounterEditorViewModel(this);

			Log.Debug($"Editor instance hash = {Editor.GetHashCode()}");

			// Initial Load of Categories (Do this once)
			var cats = PerformanceCounterCategory.GetCategories().Select(c => c.CategoryName).OrderBy(x => x);
			foreach (var cat in cats)
				Categories.Add(cat);

			// Load Existing Counters from Settings
			foreach (var settingsItem in settings.Metrics)
			{
				Log.Debug($"Metrics item type = {settingsItem?.GetType().FullName ?? "NULL"}");
				Metrics.Add(new CounterViewModel(settingsItem));
			}

			InitializeCommands();

			// Set Initial Selection
			if (Metrics.Any())
				Selected = Metrics.First();
			else 
				Editor.LoadDefaults();

			// The below is more bad then good!
			//Editor.PropertyChanged += (s, e) => { if (!IsLoading) HasPendingEdits = true; };
			// Track changes in the editor
			Editor.PropertyChanged += Editor_PropertyChanged;

			LoadIntoViewModel(settings);

		}
		*/

		//private bool _isSelecting;
		//private bool _allowReselectWithPending;
		public CounterViewModel? Selected
		{
			get => _selected;
			set => _ = SetSelectedAsync(value);
		}

		public async Task SetSelectedAsync(CounterViewModel? value)
		{
			if (_selected == value)
			{
				Log.Debug("Selected: SKIPPED (same value)");
				return;
			}

			Log.Debug($"Selected: EditorPendingEdits={EditorPendingEdits}");

			// 1. Assign first, but DO NOT notify UI yet
			_selected = value;

			if (_selected != null)
			{
				Log.Debug($"Selected: loading '{_selected.DisplayName}'");
				await ApplySelectedAsync(_selected);   // ⭐ MUST happen BEFORE PropertyChanged
			}

			// 2. NOW notify UI
			OnPropertyChanged(nameof(Selected));

			// 3. Update commands
			RefreshCommandStates();
		}

		/*
		public CounterViewModel? Selected
		{
			get => _selected;
			set
			{
				if (_selected == value)
				{
					Log.Debug($"Selected: SKIPPED _selcted == value");
					return;
				}

				Log.Debug($"Selected: _isSelecting = {_isSelecting}, HasPendingEdit = {HasPendingEdits}, _allowReselectWithPending = {_allowReselectWithPending}");

				// Global guard: don’t let selection change while there are unsaved editor edits,
				// unless a caller explicitly allows it (Discard, Reset, etc.)
				if (!_allowReselectWithPending && HasPendingEdits)
				{
					Log.Debug("Selected: SKIPPED because HasPendingEdits = true");
					return;
				}

				if (_isSelecting)
					return;
				
				_isSelecting = true;
				_selected = value;
				OnPropertyChanged();

				if (_selected != null)
				{
					Log.Debug($"CounterViewModel selected: _selected.DisplayName = '{_selected.DisplayName}'");

					LoadSelectedAsync(value);

				}
				RefreshCommandStates();     // Is needed because HasPendingEdits may not been set! 
				_isSelecting = false;
				_allowReselectWithPending = false;
			}
		}
		*/

		public void ResetEditorDirtyState()
		{
			EditorPendingEdits = false;
			IsAtDefaultConfiguration = CheckIfDefault();
		}

		private void Editor_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			Log.Debug($"Editor_PropertyChanged: IsLoading={IsLoading}" +
				$", IsSelectionLoadInProgress = {IsSelectionLoadInProgress}, Property='{e.PropertyName}'");

			// ------------------------------------------------------------
			// 1. Ignore ALL changes during programmatic loads
			// ------------------------------------------------------------
			if (IsLoading || IsSelectionLoadInProgress)
			{
				Log.Debug("Editor_PropertyChanged: suppressed (loading)");
				return;
			}

			// ------------------------------------------------------------
			// 2. Real user edits → mark editor dirty
			// ------------------------------------------------------------
			EditorPendingEdits = true;
			IsAtDefaultConfiguration = false;

			Log.Debug($"Editor_PropertyChanged: EditorPendingEdits={EditorPendingEdits}");

			// ------------------------------------------------------------
			// 3. Handle special cases
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
			// 4. Update preview + status bar
			// ------------------------------------------------------------
			UpdatePreview();
			OnPropertyChanged(nameof(MetricsCount));
			OnPropertyChanged(nameof(StatusText));

			Log.Debug($"Editor_PropertyChanged: Done... IsLoading={IsLoading}" +
				$", IsSelectionLoadInProgress = {IsSelectionLoadInProgress}");
		}

		/*
		public static SettingsOptions CreateDefault()
		{
			return new DefaultSettingsProvider().Create();
		}
		*/

		private async Task ApplySelectedAsync(CounterViewModel vm)
		{
			if (_isSelectionLoadInProgress)
			{
				Log.Debug("ApplySelectedAsync: SKIPPED (already loading)");
				return;
			}

			_isSelectionLoadInProgress = true;
			_suppressAutoSelect = true;
			BeginLoading();

			try
			{
				Log.Debug($"ApplySelectedAsync: Loading '{vm.DisplayName}'");

				// 1. Load editor FIRST
				Editor.LoadFrom(vm);

				// 2. Load lists
				await LoadCountersForCategoryAsync(Editor.SelectedCategory, _cts.Token);
				await LoadInstancesForCounterAsync(Editor.SelectedCategory, Editor.SelectedCounter, _cts.Token);

				// 3. Re-apply counter + instance
				// (safe because suppression is still ON)
				var savedCounter = vm.Counter; // the original saved value
				var counterVm = Metrics.FirstOrDefault(c => c.Counter == savedCounter);
				if (counterVm != null)
					Editor.SelectedCounter = counterVm.Counter;
				Log.Debug($"ApplySelectedAsync: Editor.SelectedCounter = {Editor.SelectedCounter}");

				var savedInstance = vm.Instance;
				if (Instances.Contains(savedInstance))
					Editor.SelectedInstance = savedInstance;
				else if (Instances.Any())
					Editor.SelectedInstance = Instances.First();
				Log.Debug($"ApplySelectedAsync: Editor.SelectedInstance = {Editor.SelectedInstance}");
			}
			finally
			{
				EndLoading();

				// Flush deferred preview update here

				if (_pendingPreviewUpdate)
				{
					_pendingPreviewUpdate = false;
					UpdatePreview();
				}

				_isSelectionLoadInProgress = false;

				// NOW it is safe to allow auto-select again
				_suppressAutoSelect = false;
			}
		}
		/*
		private bool _isLoadingSelected = false;

		private async void LoadSelectedAsync(CounterViewModel vm)
		{
			Log.Debug($"LoadSelectedAsync: _isLoadingSelected = {_isLoadingSelected}");
			if (_isLoadingSelected)
			{
				Log.Debug("LoadSelectedAsync: SKIPPED (already loading)");
				return;
			}

			_isLoadingSelected = true;

			try
			{
				Log.Debug($"LoadSelectedAsync: 1 _selected.Category = '{_selected.Category}'" +
					$", Editor._allowInstanceSetDuringLoad = {Editor._allowInstanceSetDuringLoad}");

				BeginLoading();
				DoEvents();

				await LoadCountersForCategoryAsync(vm.Category, _cts.Token);
				await LoadInstancesForCounterAsync(vm.Category, vm.Counter, _cts.Token);

				Editor.LoadFrom(vm);

				// Auto-select instance if none is selected
				if (string.IsNullOrEmpty(Editor.SelectedInstance) && Instances.Any())
				{
					Editor.SelectedInstance = Instances.First();
				}

				Log.Debug($"LoadSelectedAsync: Editor.SelectedInstance = '{Editor.SelectedInstance}'");

				// Force ComboBox to re-evaluate
				Editor._allowInstanceSetDuringLoad = true;
				Editor.SelectedInstance = Editor.SelectedInstance;
				Editor._allowInstanceSetDuringLoad = false;

				// Sync editor → model
				if (Selected != null)
				{
					Selected.Settings.ShowInTray = Editor.ShowInTray;
					OnPropertyChanged(nameof(TrayIconCount));
					OnPropertyChanged(nameof(TrayIconCountDisplay));
				}

				LoadIconSetPreviewFrames(Editor.IconSet);
				UpdatePreview();

				EndLoading();
				RefreshCommandStates();

				Log.Debug($"LoadSelectedAsync: 2 _selected.Category = '{_selected.Category}'" +
					$", Editor._allowInstanceSetDuringLoad = {Editor._allowInstanceSetDuringLoad}");

			}
			finally
			{
				_isLoadingSelected = false;

				if (!string.IsNullOrEmpty(Editor.SelectedInstance) &&
					Instances.Contains(Editor.SelectedInstance))
				{
					Editor._allowInstanceSetDuringLoad = true;
					Editor.SelectedInstance = Editor.SelectedInstance;
					Editor._allowInstanceSetDuringLoad = false;
				}

			}
			Log.Debug($"LoadSelectedAsync: 3 _selected.Category = '{_selected.Category}'" +
				$", Editor._allowInstanceSetDuringLoad = {Editor._allowInstanceSetDuringLoad}");
		}

		private static bool IsReloadTrigger(string propertyName) =>
			propertyName is nameof(Editor.SelectedCategory)
						 or nameof(Editor.SelectedCounter)
						 or nameof(Editor.SelectedInstance);
		private void Editor_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			Log.Debug($"Editor_PropertyChanged: IsLoading={IsLoading}, Suppress={SuppressEditorChanges}, ResettingConfig={_isResettingConfig}, EditorPending={EditorPendingEdits}, GlobalPending={GlobalEditsPending}");

			// ------------------------------------------------------------
			// 0. Suppression shield — programmatic updates only
			// ------------------------------------------------------------
			if (SuppressEditorChanges)
			{
				Log.Debug("Editor_PropertyChanged: suppressed due to SuppressEditorChanges");
				return;
			}

			// ------------------------------------------------------------
			// 1. Full configuration reset (ResetToDefaults)
			//    NOT used for Discard or selection reloads.
			// ------------------------------------------------------------
			if (_isResettingConfig)
			{
				// Config is being rebuilt; editor is in sync with new config.
				EditorPendingEdits = false;
				Log.Debug("Editor_PropertyChanged: inside full config reset — editor stays clean");
				return;
			}

			// ------------------------------------------------------------
			// 2. LOADING shield — ignore everything except ShowInTray
			// ------------------------------------------------------------
			if (IsLoading)
			{
				Log.Debug($"Editor_PropertyChanged: LOADING shield for '{e.PropertyName}'");

				if (e.PropertyName == nameof(Editor.ShowInTray))
					goto handleShowInTray;

				if (IsReloadTrigger(e.PropertyName))
				{
					Log.Debug("Editor_PropertyChanged: reload trigger ignored during loading");
					return;
				}

				return;
			}

			// ------------------------------------------------------------
			// 3. REAL USER EDITS (not loading, not resetting, not suppressed)
			// ------------------------------------------------------------
			EditorPendingEdits = true;
			IsAtDefaultConfiguration = false;

			Log.Debug($"Editor_PropertyChanged: REAL EDIT '{e.PropertyName}', EditorPendingEdits={EditorPendingEdits}");

			switch (e.PropertyName)
			{
				case nameof(Editor.SelectedCounter):
				case nameof(Editor.SelectedCategory):
					ReloadCounterAndInstanceListsAsync();
					break;

				case nameof(Editor.IconSet):
					Log.Debug($"IconSet changed: {Editor.IconSet}");
					LoadIconSetPreviewFrames(Editor.IconSet);
					break;

				case nameof(Editor.TrayAccentColor):
				case nameof(Editor.TrayBackgroundColor):
				case nameof(Editor.AutoTrayBackground):
				case nameof(Editor.UseTextTrayIcon):
				case nameof(Editor.ShowBackgroundColorPicker):
				case nameof(Editor.ShowIconSetSelector):
				case nameof(Editor.CurrentValue):
					// No extra action needed
					break;
			}

		handleShowInTray:
			if (e.PropertyName == nameof(Editor.ShowInTray) && Selected != null)
			{
				Log.Debug($"Editor_PropertyChanged: ShowInTray={Editor.ShowInTray}, CanShowInTray={Editor.CanShowInTray}, TrayIconCount={TrayIconCount}");

				Selected.Settings.ShowInTray = Editor.ShowInTray;
				OnPropertyChanged(nameof(TrayIconCount));
				OnPropertyChanged(nameof(TrayIconCountDisplay));
			}

			// Status bar updates
			OnPropertyChanged(nameof(MetricsCount));
			OnPropertyChanged(nameof(StatusText));
		}
		/*
		private void Editor_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			Log.Debug($"Editor_PropertyChanged: IsLoading = {IsLoading}, , _isResetting = {_isResetting}" +
				$", HasPendingEdits = {HasPendingEdits}, , HasAppliedChanges = {HasAppliedChanges}");

			if (SuppressEditorChanges)
			{
				Log.Debug("Editor_PropertyChanged: suppressed due to SuppressEditorChanges");
				return;
			}

			// 1. RESET always wins — treat everything as a user edit
			if (_isResetting)
			{
				HasPendingEdits = true;
				HasAppliedChanges = false;
				Log.Debug($"Editor_PropertyChanged: RESET wins, _isResetting = {_isResetting}" +
					$", HasPendingEdits = {HasPendingEdits}, HasAppliedChanges = {HasAppliedChanges}");

				return;
			}

			// 2. LOADING shield — block everything except ShowInTray
			if (IsLoading)
			{
				Log.Debug($"Editor_PropertyChanged: LOADING shield, PropertyName = '{e.PropertyName}'" +
					$", nameof(Editor.ShowInTray) = {nameof(Editor.ShowInTray)}");

				if (e.PropertyName == nameof(Editor.ShowInTray))
				{
					Log.Debug($"Editor_PropertyChanged: handleShowInTray");
					goto handleShowInTray;
				}

				if (IsReloadTrigger(e.PropertyName))
				{
					Log.Debug($"Editor_PropertyChanged: IsReloadTrigger");
					return;
				}

				return;
			}

			// 3. REAL user edits (not loading, not resetting)
			HasPendingEdits = true;
			IsAtDefaultConfiguration = false;

			Log.Debug($"Editor_PropertyChanged: 2 PropertyName = '{e.PropertyName}'" +
				$", HasPendingEdits = {HasPendingEdits}, IsAtDefaultConfiguration = {IsAtDefaultConfiguration}");

			switch (e.PropertyName)
			{
				case nameof(Editor.SelectedCounter):
				case nameof(Editor.SelectedCategory):
					ReloadCounterAndInstanceListsAsync();
					break;

				case nameof(Editor.IconSet):
					Log.Debug($"IconSet: {Editor.IconSet}");
					LoadIconSetPreviewFrames(Editor.IconSet);
					break;

				case nameof(Editor.TrayAccentColor):
				case nameof(Editor.TrayBackgroundColor):
				case nameof(Editor.AutoTrayBackground):
				case nameof(Editor.UseTextTrayIcon):
				case nameof(Editor.ShowBackgroundColorPicker):
				case nameof(Editor.ShowIconSetSelector):
				case nameof(Editor.CurrentValue):
					// No extra action, but still a real edit
					break;
			}

		handleShowInTray:
			Log.Debug($"Editor_PropertyChanged: ShowInTray = {Editor.ShowInTray}");
			// Update tray-limit logic
			if (e.PropertyName == nameof(Editor.ShowInTray) && Selected != null)
			{
				Log.Debug($"Editor_PropertyChanged: PropertyName = {e.PropertyName}, ShowInTray = {Editor.ShowInTray}, CanShowInTray = {Editor.CanShowInTray}, TrayIconCount = {TrayIconCount}");

				Selected.Settings.ShowInTray = Editor.ShowInTray;
				OnPropertyChanged(nameof(TrayIconCount));
				OnPropertyChanged(nameof(TrayIconCountDisplay));
			}

			// Update status bar bindings
			OnPropertyChanged(nameof(MetricsCount));
			OnPropertyChanged(nameof(StatusText));

			Log.Debug($"Editor_PropertyChanged: IsLoading = '{IsLoading}', _isResetting = {_isResetting}, HasPendingEdits = {HasPendingEdits}");
		}
		*/

		private T SafePerf<T>(Func<T> func, string context)
		{
			try
			{
				return func();
			}
			catch (Exception ex)
			{
				Log.Error(ex, $"PerfCounter error: {context}");
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
				Log.Debug($"LoadCountersForCategoryAsync: category = '{category}'");
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
				Log.Debug("LoadCountersForCategoryAsync canceled");
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Unexpected error in LoadCountersForCategoryAsync");
			}
		}

		public async Task LoadInstancesForCounterAsync(string category, string counter, CancellationToken token)
		{
			try
			{
				Log.Debug($"LoadInstancesForCounterAsync: category='{category}', counter='{counter}'");
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
						Log.Debug($"LoadInstancesForCounterAsync: Adding inst = '{inst}'");
						Instances.Add(inst);
					}

					OnPropertyChanged(nameof(Instances));
				});
			}
			catch (OperationCanceledException)
			{
				Log.Debug("LoadInstancesForCounterAsync canceled");
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Unexpected error in LoadInstancesForCounterAsync");
			}
		}

		/*
		public async Task LoadCountersForCategoryAsync(string category, CancellationToken token)
		{
			try
			{
				Log.Debug($"LoadCountersForCategoryAsync: category = '{category}'");
				token.ThrowIfCancellationRequested();

				if (string.IsNullOrEmpty(category))
					return;

				var names = await RunOnBackgroundThread(() =>
				{
					try
					{
						var cat = new PerformanceCounterCategory(category);

						string[] insts;

						try
						{
							insts = cat.GetInstanceNames();
						}
						catch
						{
							Log.Error($"GetInstanceNames failed");
							return Enumerable.Empty<string>();
						}

						if (insts.Length == 0)
						{
							try
							{
								return cat.GetCounters().Select(c => c.CounterName);
							}
							catch
							{
								Log.Error("GetCounters() failed for non-instance category");
								return Enumerable.Empty<string>();
							}
						}

						var list = new List<string>();

						foreach (var inst in insts)
						{
							try
							{
								PerformanceCounter[] counters;
								try
								{
									counters = cat.GetCounters(inst);
								}
								catch (InvalidOperationException)
								{
									Log.Error("Sampler: GetCounters failed");
									continue;
								}
								list.AddRange(counters.Select(c => c.CounterName));
							}
							catch
							{
								Log.Error($"GetCounters failed");
								continue;
							}
						}

						return list;
					}
					catch
					{
						return Enumerable.Empty<string>();
					}
				}, token);

				CountersInCategory = new ObservableCollection<string>(
					names.Distinct().OrderBy(x => x));
				OnPropertyChanged(nameof(CountersInCategory));
			}
			catch (OperationCanceledException)
			{
				// Expected during Cancel or rapid UI changes.
				Log.Debug("LoadCountersForCategoryAsync canceled");
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Unexpected error in LoadCountersForCategoryAsync");
			}
		}

		public async Task LoadInstancesForCounterAsync(string category, string counter, CancellationToken token)
		{
			try
			{
				Log.Debug($"LoadInstancesForCounterAsync: category='{category}', counter='{counter}'");
				token.ThrowIfCancellationRequested();

				if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(counter))
					return;

				// Run ALL heavy work on background thread
				var validInstances = await Task.Run(() =>
				{
					token.ThrowIfCancellationRequested();

					var result = new List<string>();

					PerformanceCounterCategory cat;
					try
					{
						cat = new PerformanceCounterCategory(category);
					}
					catch
					{
						return result;
					}

					string[] insts;
					try
					{
						insts = cat.GetInstanceNames();
					}
					catch
					{
						return result;
					}

					if (insts.Length == 0)
					{
						result.Add("");
						return result;
					}

					foreach (var inst in insts.OrderBy(x => x))
					{
						token.ThrowIfCancellationRequested();

						PerformanceCounter[] counters;
						try
						{
							counters = cat.GetCounters(inst);
						}
						catch
						{
							continue;
						}

						if (counters.Any(c => c.CounterName == counter))
							result.Add(inst);
					}

					return result;
				}, token);

				// Now marshal back to UI thread
				await Application.Current.Dispatcher.InvokeAsync(() =>
				{

					Instances.Clear();
					foreach (var inst in validInstances)
					{
						Log.Debug($"LoadInstancesForCounterAsync: Adding inst = '{inst ?? ""}'");
						Instances.Add(inst ?? "");
					}

					OnPropertyChanged(nameof(Instances));

					Log.Debug($"Instances list hash = {Instances.GetHashCode()}");
					Log.Debug($"Editor instance hash = {Editor.GetHashCode()}");

				});
			}
			catch (OperationCanceledException)
			{
				// Expected during Cancel or rapid UI changes.
				Log.Debug("LoadInstancesForCounterAsync canceled");
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Unexpected error in LoadInstancesForCounterAsync");
			}
		}

		private async void ReloadCounterAndInstanceListsAsync()
		{
			Log.Debug($"ReloadCounterAndInstanceListsAsync: 1 Editor.SelectedCategory = '{Editor.SelectedCategory}', Editor.SelectedCounter = '{Editor.SelectedCounter}'");

			BeginLoading();

			await LoadCountersForCategoryAsync(Editor.SelectedCategory, _cts.Token);

			EndLoading(); // parent loading is about counters only

			Log.Debug($"ReloadCounterAndInstanceListsAsync: 2 Editor.SelectedCategory = '{Editor.SelectedCategory}', Editor.SelectedCounter = '{Editor.SelectedCounter}'");

			// Auto-select first counter AFTER loading finished
			if (string.IsNullOrWhiteSpace(Editor.SelectedCounter) && CountersInCategory.Any())
			{
				var first = CountersInCategory.First();
				Log.Debug($"ReloadCounterAndInstanceListsAsync: Auto-selecting first counter AFTER loading finished, First Counter = '{first}'");
				Editor.SelectedCounter = first; // this will trigger LoadInstancesAsync and its own auto-select
			}
		}
		*/

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
		/*
		private void InitializeCommands()
		{

			ApplyCommand = new RelayCommand(_ => ApplyCounter(), _ => HasPendingEdits);
			CancelCommand = new RelayCommand(
				_ => CancelEdits(),
				_ => HasPendingEdits || HasAppliedChanges
			);
			CloseCommand = new RelayCommand(_ => CloseWindow());
			CopyCommand = new RelayCommand(_ => CopySelectedMetric(), _ => Selected != null);
			DiscardCommand = new RelayCommand(_ => DiscardEdits(), _ => HasPendingEdits);
			OpenDebugIconWindowCommand = new RelayCommand(_ => OpenDebugIconWindow());
			RemoveCommand = new RelayCommand(_ => RemoveSelected(), _ => Selected != null);
			ResetCommand = new RelayCommand(
				_ => { if (ConfirmReset?.Invoke() ?? true) ResetToDefaults(); },
				_ => !IsAtDefaultConfiguration
			);
			SaveCommand = new RelayCommand(_ => Save(), _ => HasPendingEdits || HasAppliedChanges);
			ShowMinMaxInfoCommand = new RelayCommand(_ => ShowMinMaxInfo());
		}
		*/

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

			Log.Debug($"ApplyCounter: Selected='{Selected.DisplayName}', EditorPending={EditorPendingEdits}" +
			  $", Selected.Id = {Selected.Id}, Editor.Id = {Editor.Id}, editingExisting = {Selected.Id == Editor.Id}");

			if (IsEditingExistingMetric())
			{
				// Update the existing metric
				ApplyEditorToSelected();
			}
			else
			{
				// Create a new metric
				var settings = Editor.ToSettings();
				settings.Id = Guid.NewGuid();

				var vm = new CounterViewModel(settings);
				Metrics.Add(vm);

				Selected = vm;
				EditorPendingEdits = false;
			}

			GlobalEditsPending = true;

			UpdateUiState();

			Log.Debug($"ApplyCounter: DONE — EditorPending={EditorPendingEdits}, GlobalPending={GlobalEditsPending}");
		}

		private void UpdateUiState()
		{
			OnPropertyChanged(nameof(MetricsCount));
			OnPropertyChanged(nameof(TrayIconCount));
			OnPropertyChanged(nameof(TrayIconCountDisplay));

			IsAtDefaultConfiguration = CheckIfDefault();
		}

		/*
		private void ApplyCounter()
		{
			// 1. Determine if the editor is showing an existing metric
			bool editorIsEditingExistingMetric =
				Selected != null &&
				Selected.Settings.Id == Editor.Id;

			// 2. Apply editor changes to the existing metric (if needed)
			if (editorIsEditingExistingMetric && HasPendingEdits)
				ApplyEditorToSelected();

			// 3. Create a new metric from the editor state
			var settings = Editor.ToSettings();
			settings.Id = Guid.NewGuid();

			var vm = new CounterViewModel(settings);

			// 4. Add to UI + persistent settings
			Counters.Add(vm);
			_globalSettings.Metrics.Add(settings);

			// 5. Select the new metric ONLY if we were editing an existing one
			if (editorIsEditingExistingMetric)
				Selected = vm;

			IsAtDefaultConfiguration = CheckIfDefault();

			// 6. Reset pending edits
			HasPendingEdits = false;

			OnPropertyChanged(nameof(MetricsCount));
			OnPropertyChanged(nameof(TrayIconCount));
			OnPropertyChanged(nameof(TrayIconCountDisplay));
		}
		*/

		private void ApplyEditorToSelected()
		{
			if (Selected == null)
				return;

			var settings = Editor.ToSettings();
			Log.Debug($"ApplyEditorToSelected: DisplayName='{settings.DisplayName}'");

			Selected.UpdateFromSettings(settings);

			// Editor is now clean
			EditorPendingEdits = false;

			// Update snapshot AFTER committing changes
			//SaveSnapshot();
		}
		/*
		private void ApplyEditorToSelected()
		{
			if (Selected == null)
				return;

			var settings = Editor.ToSettings();
			Log.Debug($"ApplyEditorToSelected: .DisplayName = '{settings.DisplayName}'");

			Selected.UpdateFromSettings(settings); // Assume this method updates the VM properties
			HasPendingEdits = false;
			//RefreshCommandStates();	<== Included in HasPendingEdits!
		}
		*/

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
		/*
		private void CopySelectedMetric()
		{
			if (Selected == null)
				return;

			Editor.LoadFrom(Selected);

			Editor.Id = Guid.NewGuid();
			Editor.DisplayName = GenerateCopyName(Selected.DisplayName);

			HasPendingEdits = true;
		}
		*/

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
		/*
		void CancelEdits()
		{
			if (!(ConfirmCancel?.Invoke() ?? true))
				return;

			LoadSettingsFromDisk();

			HasPendingEdits = false;
			HasAppliedChanges = false;
			IsAtDefaultConfiguration = CheckIfDefault();

			RequestWindowClose();
		}
		*/
		private bool CheckIfDefault()
		{
			var defaults = SettingsOptions.CreateDefault();

			var currentDto = SettingsMapper.ToDto(_globalSettings);
			var defaultDto = SettingsMapper.ToDto(defaults);

			Log.Debug($"CheckIfDefault: Result = {currentDto.Metrics.Count != defaultDto.Metrics.Count}");
			if (currentDto.Metrics.Count != defaultDto.Metrics.Count)
				return false;

			for (int i = 0; i < currentDto.Metrics.Count; i++)
			{
				Log.Debug($"CheckIfDefault: Result.{i} = {!currentDto.Metrics[i].IsEquivalentToDefault(defaultDto.Metrics[i])}");
				if (!currentDto.Metrics[i].IsEquivalentToDefault(defaultDto.Metrics[i]))
					return false;
			}

			return true;
		}

		private void RemoveSelected()
		{
			if (Selected == null)
				return;

			Log.Debug($"RemoveSelected: Metrics.Count = {Metrics.Count}");

			var toRemove = Selected;
			int idx = Metrics.IndexOf(toRemove);

			Metrics.Remove(toRemove);

			if (Metrics.Any())
			{
				// Select the next logical item
				Selected = Metrics[Math.Min(idx, Metrics.Count - 1)];
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

			Log.Debug($"RemoveSelected: Metrics.Count = {Metrics.Count}");
		}

		/*
		private void RemoveSelected()
		{
			if (Selected == null)
			{
				Log.Debug("RemoveSelected: SKIPPED (Selected is NULL)");
				return;
			}

			// Block ALL selection changes during removal

			var toRemove = Selected;
			int idx = Metrics.IndexOf(toRemove);

			Metrics.Remove(toRemove);

			var settings = _globalSettings.Metrics
				.FirstOrDefault(m => m.Id == toRemove.Settings.Id);

			if (settings != null)
				_globalSettings.Metrics.Remove(settings);

			Log.Debug($"RemoveSelected: Metrics.Count = {Metrics.Count}");

			if (Counters.Any())
			{
				// Now safe: collection cannot change during this assignment
				Selected = Metrics[Math.Min(idx, Metrics.Count - 1)];
			}
			else
			{
				Editor.LoadDefaults();

				EditorPendingEdits = false;

				_selected = null;
				OnPropertyChanged(nameof(Selected));
			}

			GlobalEditsPending = true;

			OnPropertyChanged(nameof(MetricsCount));
			OnPropertyChanged(nameof(TrayIconCount));
			OnPropertyChanged(nameof(TrayIconCountDisplay));

			IsAtDefaultConfiguration = CheckIfDefault();

			// Update snapshot AFTER the removal is fully applied
			//SaveSnapshot();
		}

		private void RemoveSelected()
		{
			if (Selected == null)
				return;

			if (_isSelecting)
			{
				Log.Debug("RemoveSelected: SKIPPED (selection already in progress)");
				return;
			}

			var toRemove = Selected;

			Log.Debug($"RemoveSelected: Removing '{toRemove.DisplayName}'");

			// ------------------------------------------------------------
			// 1. Remove from UI list
			// ------------------------------------------------------------
			int idx = Counters.IndexOf(toRemove);
			Counters.Remove(toRemove);

			// ------------------------------------------------------------
			// 2. Remove from persistent settings
			// ------------------------------------------------------------
			var settings = _globalSettings.Metrics
				.FirstOrDefault(m => m.Id == toRemove.Settings.Id);

			if (settings != null)
				_globalSettings.Metrics.Remove(settings);

			// ------------------------------------------------------------
			// 3. Update selection or reset editor
			// ------------------------------------------------------------
			Log.Debug($"RemoveSelected: Counters.Count = {Counters.Count}");
			_isSelecting = true;
			_allowReselectWithPending = true;

			if (Counters.Any())
			{
				Selected = Counters[Math.Min(idx, Counters.Count - 1)];
			}
			else
			{
				SuppressEditorChanges = true;
				Editor.LoadDefaults();
				SuppressEditorChanges = false;

				EditorPendingEdits = false;

				_selected = null;
				OnPropertyChanged(nameof(Selected));
			}

			Log.Debug($"RemoveSelected: Counters.Count = {Counters.Count}");
			_allowReselectWithPending = false;
			_isSelecting = false;

			// ------------------------------------------------------------
			// 4. Removing a metric is a GLOBAL change
			// ------------------------------------------------------------
			GlobalEditsPending = true;

			// ------------------------------------------------------------
			// 5. Update UI
			// ------------------------------------------------------------
			OnPropertyChanged(nameof(MetricsCount));
			OnPropertyChanged(nameof(TrayIconCount));
			OnPropertyChanged(nameof(TrayIconCountDisplay));

			IsAtDefaultConfiguration = CheckIfDefault();

			Log.Debug($"RemoveSelected: DONE — GlobalPending={GlobalEditsPending}, EditorPending={EditorPendingEdits}");
		}
		
		private void RemoveSelected()
		{
			if (Selected == null)
				return;

			var toRemove = Selected;

			// 1. Remove from UI list
			int idx = Counters.IndexOf(toRemove);
			Counters.Remove(toRemove);

			// 2. Remove from persistent settings
			var settings = _globalSettings.Metrics
				.FirstOrDefault(m => m.Id == toRemove.Settings.Id);

			if (settings != null)
				_globalSettings.Metrics.Remove(settings);

			// 3. Update selection or reset editor
			if (Counters.Any())
				Selected = Counters[Math.Min(idx, Counters.Count - 1)];
			else
				Editor.LoadDefaults();

			HasPendingEdits = true;

			OnPropertyChanged(nameof(MetricsCount));
			OnPropertyChanged(nameof(TrayIconCount));
			OnPropertyChanged(nameof(TrayIconCountDisplay));
		}
		*/

		private void Save()
		{
			Log.Debug($"Save: Started Selected={Selected}, EditorPending={EditorPendingEdits}, " +
					  $"GlobalPending={GlobalEditsPending}, Metrics.Count = {Metrics.Count}");

			// 1. Apply editor changes if needed
			if (Selected != null && EditorPendingEdits)
				Selected.UpdateFromSettings(Editor.ToSettings());

			// 2. Snapshot BEFORE writing to disk (for Cancel)
			SaveSnapshot();

			// 3. Build a fresh SettingsOptions from the ViewModel
			var newSettings = SettingsMapper.FromViewModel(this);

			// 4. Convert to DTO and enqueue async save
			var dto = SettingsMapper.ToDto(newSettings);
			SettingsSaveQueue.Enqueue(dto);

			// 5. Replace runtime settings with the NEW settings
			_main.ReplaceSettings(newSettings);

			// 6. Clear dirty flags
			GlobalEditsPending = false;
			EditorPendingEdits = false;

			// 7. Recompute default-state flag
			IsAtDefaultConfiguration = CheckIfDefault();

			Log.Debug($"Save: DONE — GlobalEditsPending={GlobalEditsPending}, " +
					  $"EditorPendingEdits={EditorPendingEdits}, " +
					  $"IsAtDefault={IsAtDefaultConfiguration}, Metrics.Count= {Metrics.Count}");
		}
		/*
		private void Save()
		{
			Log.Debug($"Save: Started Selected={Selected}, EditorPending={EditorPendingEdits}, " +
					  $"GlobalPending={GlobalEditsPending}, Metrics.Count = {Metrics.Count}");

			// 1. Apply editor changes if needed
			if (Selected != null && EditorPendingEdits)
				ApplyEditorToSelected();

			// 2. Snapshot BEFORE writing to disk (for Cancel)
			SaveSnapshot();

			// 3. Build a fresh SettingsOptions from the ViewModel
			var newSettings = SettingsMapper.FromViewModel(this);

			// 4. Convert to DTO and enqueue async save
			var dto = SettingsMapper.ToDto(newSettings);
			SettingsSaveQueue.Enqueue(dto);

			// 5. Replace runtime settings with the NEW settings
			_main.ReplaceSettings(newSettings);

			// 6. Clear dirty flags
			GlobalEditsPending = false;
			EditorPendingEdits = false;

			// 7. Recompute default-state flag
			IsAtDefaultConfiguration = CheckIfDefault();

			Log.Debug($"Save: DONE — GlobalEditsPending={GlobalEditsPending}, " +
					  $"EditorPendingEdits={EditorPendingEdits}, " +
					  $"IsAtDefault={IsAtDefaultConfiguration}, Metrics.Count= {Metrics.Count}");
		}

		private void Save()
		{
			Log.Debug($"Save: Started Selected={Selected}, EditorPending={EditorPendingEdits}" +
				$", GlobalPending={GlobalEditsPending}, Metrics.Count = {Metrics.Count}");

			// ------------------------------------------------------------
			// 1. If the editor is dirty, apply its changes first
			// ------------------------------------------------------------
			//if (Selected != null && EditorPendingEdits)
			//	ApplyEditorToSelected();   // This clears EditorPendingEdits

			// Snapshot BEFORE writing to disk
			SaveSnapshot();

			// ------------------------------------------------------------
			// 2. Convert runtime settings → DTO
			// ------------------------------------------------------------
			var dto = SettingsMapper.ToDto(_globalSettings);

			// ------------------------------------------------------------
			// 3. Enqueue async save + update tray icons
			// ------------------------------------------------------------
			SettingsSaveQueue.Enqueue(dto);
			_main.ReplaceSettings(_globalSettings);

			// ------------------------------------------------------------
			// 4. Update UI status bar
			// ------------------------------------------------------------
			OnPropertyChanged(nameof(MetricsCount));
			OnPropertyChanged(nameof(TrayIconCount));
			OnPropertyChanged(nameof(TrayIconCountDisplay));
			OnPropertyChanged(nameof(StatusText));

			// ------------------------------------------------------------
			// 5. Clear global dirty state
			// ------------------------------------------------------------
			GlobalEditsPending = false;

			// EditorPendingEdits should already be false here
			// because ApplyEditorToSelected() clears it.

			// ------------------------------------------------------------
			// 6. Recompute default-state flag
			// ------------------------------------------------------------
			IsAtDefaultConfiguration = CheckIfDefault();

			Log.Debug($"Save: DONE — GlobalEditsPending={GlobalEditsPending}" +
				$", EditorPendingEdits={EditorPendingEdits}" +
				$", IsAtDefault={IsAtDefaultConfiguration}, Metrics.Count= {Metrics.Count}");
		}

		private void Save()
		{
			Log.Debug($"Save: Selected = {Selected}, HasPendingEdits = {HasPendingEdits}");

			// Apply pending edits to the selected metric
			if (Selected != null && HasPendingEdits)
				ApplyEditorToSelected();

			// Convert runtime settings → DTO
			var dto = SettingsMapper.ToDto(_globalSettings);

			// Enqueue async save
			SettingsSaveQueue.Enqueue(dto);
			// Tell TrayIconManager to reset the icons
			_main.ReplaceSettings(_globalSettings);

			// Update status bar
			OnPropertyChanged(nameof(MetricsCount));
			OnPropertyChanged(nameof(TrayIconCount));
			OnPropertyChanged(nameof(TrayIconCountDisplay));
			OnPropertyChanged(nameof(StatusText));

			// Clear dirty flags
			HasPendingEdits = false;
			HasAppliedChanges = false;
			IsAtDefaultConfiguration = CheckIfDefault();

			//RequestClose?.Invoke();
			//_previewTimer?.Stop();
		}
		*/

		private void DiscardEdits()
		{
			if (Selected == null)
			{
				Log.Debug("DiscardEdits: Nothing to do!");
				return;
			}

			Log.Debug($"DiscardEdits: Restoring editor for '{Selected.DisplayName}'");


			// ------------------------------------------------------------
			// 2. Reload the editor directly from the selected metric
			//    (no selection clearing, no resetting, no pipeline)
			// ------------------------------------------------------------
			Editor.LoadFrom(Selected);

			// ------------------------------------------------------------
			// 3. Editor is now clean
			// ------------------------------------------------------------
			EditorPendingEdits = false;

			// ------------------------------------------------------------
			// 4. Global state is untouched
			// ------------------------------------------------------------
			// GlobalEditsPending stays whatever it was.

			// ------------------------------------------------------------
			// 6. Update UI
			// ------------------------------------------------------------
			IsAtDefaultConfiguration = CheckIfDefault();
			RefreshCommandStates();

			Log.Debug($"DiscardEdits: DONE — EditorPendingEdits={EditorPendingEdits}, GlobalEditsPending={GlobalEditsPending}");
		}
		/*
		private void DiscardEdits()
		{
			if (Selected == null)
			{
				Log.Debug($"DiscardEdits: Nothing to do!");
				return;
			}

			Log.Debug($"DiscardEdits: Selected.DisplayName = '{Selected.DisplayName}'");
			var current = Selected;

			// 1a. Mark as resetting BEFORE anything else
			_isResetting = true;
			
			// 1a. Suppress ALL editor property changes during restore
			SuppressEditorChanges = true;

			// 2. Clear selection
			_selected = null;
			OnPropertyChanged(nameof(Selected));


			// 3. Restore editor state BEFORE selection pipeline
			Editor._allowInstanceSetDuringLoad = true;
			Editor.LoadFrom(current);
			Editor._allowInstanceSetDuringLoad = false;

			// 4. Allow reselect even though HasPendingEdits was true
			_allowReselectWithPending = true;

			// 5. Trigger selection pipeline (loads counters + instances)
			Selected = current;

			// 6a. End suppression AFTER selection pipeline is triggered
			SuppressEditorChanges = false;

			// 6b. End resetting
			_isResetting = false;

			// 7. Clear pending edits
			HasPendingEdits = false;

			// 8. Recompute default-state flag
			IsAtDefaultConfiguration = CheckIfDefault();

			RefreshCommandStates();
			Log.Debug($"DiscardEdits: HasPendingEdits = {HasPendingEdits}, IsAtDefaultConfiguration = {IsAtDefaultConfiguration}");
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
			Log.Debug("LoadIntoViewModel: rebuilding UI from settings");

			// ------------------------------------------------------------
			// 1. Replace underlying settings
			// ------------------------------------------------------------
			_globalSettings = settings;
			GlobalSettings = settings;

			// ------------------------------------------------------------
			// 2. Rebuild metric list
			// ------------------------------------------------------------
			Metrics.Clear();
			foreach (var settingsItem in settings.Metrics)
			{
				Log.Debug($"Metrics item type = {settingsItem?.GetType().FullName ?? "NULL"}");
				Metrics.Add(new CounterViewModel(settingsItem));
			}

			// ------------------------------------------------------------
			// 3. Select the first metric (or clear editor)
			// ------------------------------------------------------------
			_selected = null;
			OnPropertyChanged(nameof(Selected));

			Selected = Metrics.FirstOrDefault();

			// ------------------------------------------------------------
			// 4. Reset dirty flags (this is ALWAYS a full reload)
			// ------------------------------------------------------------
			GlobalEditsPending = false;
			EditorPendingEdits = false;

			// ------------------------------------------------------------
			// 5. Recompute default-state flag
			// ------------------------------------------------------------
			IsAtDefaultConfiguration = CheckIfDefault();

			RefreshCommandStates();

			Log.Debug($"LoadIntoViewModel: DONE — GlobalEditsPending={GlobalEditsPending}, EditorPendingEdits={EditorPendingEdits}, IsAtDefault={IsAtDefaultConfiguration}");
		}

		/*
		private void LoadIntoViewModel(SettingsOptions settings)
		{
			// Copy settings into your ViewModel properties
			_globalSettings = settings;
			GlobalSettings = settings;

			// Rebuild metric list, selection, editor, etc.
			Metrics.Clear();
			foreach (var m in settings.Metrics)
				Metrics.Add(new CounterViewModel(m));

			// Force selection change so editor reloads even if same metric
			_selected = null;
			OnPropertyChanged(nameof(Selected));

			Selected = Metrics.FirstOrDefault();

			IsAtDefaultConfiguration = CheckIfDefault();

			Log.Debug($"LoadIntoViewModel: _isResetting = {_isResetting}, HasPendingEdits = {HasPendingEdits}, HasAppliedChanges = {HasAppliedChanges}");
			// Reset dirty flags ONLY if this is a normal load
			if (!_isResetting)
			{
				HasPendingEdits = false;
				HasAppliedChanges = false;
			}
			IsAtDefaultConfiguration = CheckIfDefault();
		}
		*/

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
				Interval = TimeSpan.FromSeconds(1.0)
			};

			_previewTimer.Tick += (_, __) => UpdateDynamicPreview();
			_previewTimer.Start();
		}

		public void StopPreviewTimer()
		{
			_previewTimer?.Stop();
		}

		/*
		public bool HasPendingEdits
		{
			get => _hasPendingEdits;
			private set
			{
				_hasPendingEdits = value;
				OnPropertyChanged();
				RefreshCommandStates();
			}
		}
		*/

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
			Log.Debug($"ResetToDefaults: Starting... Metric count = {Metrics.Count}");

			BeginLoading();
			_isResettingConfig = true;

			try
			{
				var defaults = SettingsOptions.CreateDefault();

				// 0. Clear selection BEFORE clearing the list
				Selected = null;

				// 1. Replace the entire metrics list
				Metrics.Clear();
				foreach (var m in defaults.Metrics)
					Metrics.Add(new CounterViewModel(m));

				// 2. Select the first metric
				Selected = Metrics.FirstOrDefault();

				// 3. Load editor
				if (Selected != null)
					Editor.LoadFrom(Selected);

				GlobalEditsPending = true;
				EditorPendingEdits = false;
				IsAtDefaultConfiguration = true;

			}
			finally
			{
				_isResettingConfig = false;
				EndLoading();
				Log.Debug($"ResetToDefaults: Completed..., Metric count = {Metrics.Count}");
			}
		}
		/*
		private void ResetToDefaults()
		{
			BeginLoading();
			_isResetting = true;

			try
			{
				// 1. Load real defaults from your provider
				var defaults = SettingsOptions.CreateDefault();

				// 2. Replace underlying settings
				_globalSettings = SettingsOptions.CreateDefault(); 
				//CurrentSettings = defaults;

				// 3. Rebuild the UI from defaults
				LoadIntoViewModel(defaults);

				IsAtDefaultConfiguration = true;

				// 4. Mark as dirty so Save/Cancel enable
				HasPendingEdits = true;
				HasAppliedChanges = false;

				// 5. Reset button should now be disabled
				IsAtDefaultConfiguration = true;
			}
			finally
			{

				// Delay clearing the reset flag until AFTER the UI thread
				// has processed all Editor property changes.
				Application.Current.Dispatcher.InvokeAsync(() =>
				{
					_isResetting = false;
					EndLoading();
				}, DispatcherPriority.Background);
			}

			Log.Debug($"ResetToDefaults: HasPendingEdits = {HasPendingEdits}, HasAppliedChanges = {HasAppliedChanges}, Result = {HasPendingEdits || HasAppliedChanges}");
		}
		*/

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
			Log.Debug($"SaveSnapshot: Metric count = {Metrics.Count}");
			_lastSavedMetricsSnapshot = GlobalSettings.Metrics
				.Select(SettingsMapper.ToCounterDto)
				.ToList();
		}

		private void LoadIconSetPreviewFrames(string? iconSetName)
		{
			Log.Debug($"iconSetName: {iconSetName}");
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

		public void UpdatePreview()
		{
			if (IsLoading || IsSelectionLoadInProgress)
			{
				_pendingPreviewUpdate = true;
				return;
			}

			// Always read from the Editor, not from ConfigViewModel
			if (!Editor.UseTextTrayIcon)
			{
				TrayPreviewImage = IconSetPreviewFrames?[CurrentFrameIndex];
				return;
			}

			double mid = (Editor.Min + Editor.Max) / 2.0;
			string text = FormatValueForTray(mid);

			var accent = Editor.TrayAccentColor;

			var bg = Editor.AutoTrayBackground
				? UIColors.GetTrayBackground(accent.ToDrawingColor(), autoContrast: true)
				: Editor.TrayBackgroundColor.ToDrawingColor();

			double dpiScale = 1.0;

			TrayPreviewImage = TrayIconGenerator.CreateTextBitmapSource(
				text,
				accent.ToDrawingColor(),
				bg,
				dpiScale);

			Log.Debug($"Preview updated: {TrayPreviewImage != null}");
		}

		private void UpdateDynamicPreview()
		{
			// 1. Generate a random metric value
			int min = (int)Math.Floor(Editor.Min);
			int max = (int)Math.Ceiling(Editor.Max);
			if (max < min)
				max = min;
			double value = _random.Next(min, max + 1);

			// 2. Text mode
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

			// 3. Icon-set mode
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
