using PerformanceTrayMonitor.Common;
using PerformanceTrayMonitor.Configuration;
using PerformanceTrayMonitor.Extensions;
using PerformanceTrayMonitor.Models;
using PerformanceTrayMonitor.Settings;
using PerformanceTrayMonitor.Tray;
using System;
using System.Windows;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
		private bool _pendingPreviewUpdate;

		// ============================================================
		//  INTERNAL SHIELDS (minimal, intentional)
		// ============================================================

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
				// Flush pending preview updates when loading finishes
				if (!_isLoading && _pendingPreviewUpdate)
				{
					_pendingPreviewUpdate = false;
					UpdateDynamicPreview();
				}
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

		//private bool _isSelectionLoadInProgress = false;
		//internal bool IsSelectionLoadInProgress => _isSelectionLoadInProgress;

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
				if (!_isSelectionLoadInProgress && _pendingPreviewUpdate)
				{
					_pendingPreviewUpdate = false;
					UpdateDynamicPreview();
				}
			}
		}

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

			var cats = PerformanceCounterCategory
				.GetCategories()
				.Select(c => c.CategoryName)
				.OrderBy(x => x);

			foreach (var cat in cats)
				Categories.Add(cat);

			// Load existing metrics from GlobalSettings
			foreach (var metricDto in GlobalSettings.Metrics)
			{
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

		public CounterViewModel? Selected
		{
			get => _selected;
			set => _ = SetSelectedAsync(value);
		}

		public async Task SetSelectedAsync(CounterViewModel? value)
		{
			if (_selected == value)
			{
				return;
			}

			// Assign first, but DO NOT notify UI yet
			_selected = value;

			if (_selected != null)
			{
				await ApplySelectedAsync(_selected);   // ⭐ MUST happen BEFORE PropertyChanged
			}

			// NOW notify UI
			OnPropertyChanged(nameof(Selected));

			// Update commands
			RefreshCommandStates();
		}

		public void ResetEditorDirtyState()
		{
			EditorPendingEdits = false;
			IsAtDefaultConfiguration = CheckIfDefault();
		}

		private void Editor_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
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

		private async Task ApplySelectedAsync(CounterViewModel vm)
		{
			if (IsSelectionLoadInProgress)
			{
				return;
			}

			IsSelectionLoadInProgress = true;
			_suppressAutoSelect = true;
			BeginLoading();

			try
			{
				// Load editor FIRST
				Editor.LoadFrom(vm);

				// Load lists
				await LoadCountersForCategoryAsync(Editor.SelectedCategory, _cts.Token);
				await LoadInstancesForCounterAsync(Editor.SelectedCategory, Editor.SelectedCounter, _cts.Token);

				// Re-apply counter + instance
				// (safe because suppression is still ON)
				var savedCounter = vm.Counter; // the original saved value
				var counterVm = Metrics.FirstOrDefault(c => c.Counter == savedCounter);
				if (counterVm != null)
					Editor.SelectedCounter = counterVm.Counter;

				var savedInstance = vm.Instance;
				if (Instances.Contains(savedInstance))
					Editor.SelectedInstance = savedInstance;
				else if (Instances.Any())
					Editor.SelectedInstance = Instances.First();
			}
			finally
			{
				EndLoading();

				IsSelectionLoadInProgress = false;
				/* Flush deferred preview update here
				if (_pendingPreviewUpdate)
				{
					_pendingPreviewUpdate = false;
					UpdateDynamicPreview();
				}
				*/
				LoadIconSetPreviewFrames(Editor.IconSet);
				//UpdateDynamicPreview();

				// NOW it is safe to allow auto-select again
				_suppressAutoSelect = false;
			}
		}

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
		}

		private void UpdateUiState()
		{
			OnPropertyChanged(nameof(MetricsCount));
			OnPropertyChanged(nameof(TrayIconCount));
			OnPropertyChanged(nameof(TrayIconCountDisplay));

			IsAtDefaultConfiguration = CheckIfDefault();
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
			// Global state is untouched
			// ------------------------------------------------------------
			// GlobalEditsPending stays whatever it was.

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
			_isResettingConfig = true;

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
				_isResettingConfig = false;
				EndLoading();
			}
		}

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

		/*
		public void UpdatePreview()
		{
			if (IsLoading || IsSelectionLoadInProgress)
			{
				Log.Debug($"UpdatePreview: Early exit, no preview! IsLOading = {IsLoading}, IsSelectionLoadInProgress = {IsSelectionLoadInProgress}");
				_pendingPreviewUpdate = true;
				return;
			}

			Log.Debug($"UpdatePreview: Editor.UseTestTrayIcon = {Editor.UseTextTrayIcon}");
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
		}
		*/

		private void UpdateDynamicPreview()
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
