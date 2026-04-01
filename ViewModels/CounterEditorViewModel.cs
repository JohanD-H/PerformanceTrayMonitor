using PerformanceTrayMonitor.Common;
using PerformanceTrayMonitor.Configuration;
using PerformanceTrayMonitor.Models;
using PerformanceTrayMonitor.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static PerformanceTrayMonitor.ViewModels.ConfigViewModel;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;

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
		public bool CanShowInTray => _parent.TrayIconCount < TrayIconConfig.MaxCounterTrayIcons || ShowInTray;
		public bool ShowInTray
		{
			get => _showInTray;
			set
			{
				_showInTray = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(ShowIconSetSelector));
			}
		}
		public string IconSet { get => _iconSet; set { _iconSet = value; OnPropertyChanged(); } }
		public ICommand PickAccentColorCommand { get; }
		public ICommand PickBackgroundColorCommand { get; }
		public double CurrentValue { get; set; } = 0;

		private bool _loadingFromModel;

		private string _category, _counter, _instance, _displayName;
		private float _min, _max;
		private bool _showInTray;
		private string _iconSet;
		private bool _useTextTrayIcon;
		private System.Windows.Media.Color _trayAccentColor;
		private bool _autoTrayBackground;
		private System.Windows.Media.Color _trayBackgroundColor;

		public Brush TrayAccentBrush => new SolidColorBrush(TrayAccentColor);
		public Brush TrayBackgroundBrush => new SolidColorBrush(TrayBackgroundColor);

		private string _uiCategory;
		private string _uiCounter;
		private string _uiInstance;
		
		internal bool _suppressEditorSetters;

		public CounterEditorViewModel(ConfigViewModel parent)
		{
			_parent = parent;

			PickAccentColorCommand = new RelayCommand(_ => PickAccentColor());
			PickBackgroundColorCommand = new RelayCommand(_ => PickBackgroundColor());
		}

		/*
		private async Task LoadListAndSelectAsync(
			Func<CancellationToken, Task> loadFunc,
			Func<bool> isLoadValid,
			Func<IEnumerable<string>> getList,
			Func<string> getCurrentSelection,
			Action<string> setSelection)
		{
			if (!isLoadValid())
			{
				setSelection("");
				return;
			}

			_parent._instanceLoadCts?.Cancel();
			_parent._instanceLoadCts = new CancellationTokenSource();
			var token = _parent._instanceLoadCts.Token;

			//await loadFunc(token);
			// Move the heavy load OFF the UI thread
			await Task.Run(async () => await loadFunc(token), token);

			if (_loadingFromModel)
				return;

			//var list = getList();
			// Also evaluate list + selection off the UI thread
			var list = await Task.Run(() => getList(), token);
			var current = getCurrentSelection();
			// Only update UI-bound properties on UI thread
			Application.Current.Dispatcher.Invoke(() =>
			{
				if (list.Contains(current))
					setSelection(current);
				else if (list.Any())
					setSelection(list.First());
				else
					setSelection("");
			});
		}

		private void LoadListAndSelectAsyncFireAndForget(
			Func<CancellationToken, Task> loadFunc,
			Func<bool> isLoadValid,
			Func<IEnumerable<string>> getList,
			Func<string> getCurrentSelection,
			Action<string> setSelection)
		{
			_ = Task.Run(async () =>
			{
				if (!isLoadValid())
				{
					Application.Current.Dispatcher.Invoke(() => setSelection(""));
					return;
				}

				_parent._instanceLoadCts?.Cancel();
				_parent._instanceLoadCts = new CancellationTokenSource();
				var token = _parent._instanceLoadCts.Token;

				await loadFunc(token);

				if (_loadingFromModel)
					return;

				var list = getList();
				var current = getCurrentSelection();

				Application.Current.Dispatcher.Invoke(() =>
				{
					if (list.Contains(current))
						setSelection(current);
					else if (list.Any())
						setSelection(list.First());
					else
						setSelection("");
				});
			});
		}

		private async Task LoadCountersAsync(string category)
		{
			// If no reloads then use:
			//return Task.CompletedTask;

			if (string.IsNullOrWhiteSpace(category))
				return;

			var counters = await _parent.LoadCountersCoreAsync(category, CancellationToken.None);

			_parent.CountersInCategory.Clear();
			foreach (var c in counters)
				_parent.CountersInCategory.Add(c);

			// Do NOT auto-select here
		}

		private Task LoadCountersAsync(string category)
		{
			return LoadListAndSelectAsync(
				loadFunc: token => _parent.LoadCountersForCategoryAsync(category, token),
				isLoadValid: () => true,
				getList: () => _parent.CountersInCategory,
				getCurrentSelection: () => SelectedCounter,
				setSelection: v => SelectedCounter = v
			);
		}
		*/

		private async Task LoadInstancesAsync(string counter)
		{
			// If no reloads then use
			//return Task.CompletedTask;

			if (string.IsNullOrWhiteSpace(counter) || string.IsNullOrWhiteSpace(_uiCategory))
				return;

			var instances = await _parent.LoadInstancesCoreAsync(_uiCategory, counter, CancellationToken.None);

			_parent.Instances.Clear();
			foreach (var inst in instances)
				_parent.Instances.Add(inst);

			// Do NOT auto-select here
		}

		/*
		private Task LoadInstancesAsync(string counter)
		{
			return LoadListAndSelectAsync(
				loadFunc: token => _parent.LoadInstancesForCounterAsync(_category, counter, token),
				isLoadValid: () =>
					!string.IsNullOrWhiteSpace(counter) &&
					!string.IsNullOrWhiteSpace(_category),
				getList: () => _parent.Instances,
				getCurrentSelection: () => _instance,
				setSelection: v => SelectedInstance = v
			);
		}

		private async Task LoadWithBusyAsync(Func<string, Task> loadFunc, string arg)
		{
			_parent.BeginLoading();
			try
			{
				await loadFunc(arg);
			}
			finally
			{
				_parent.EndLoading();
			}
		}

		private void SetAndMaybeLoad(
			ref string field,
			string? newValue,
			string propertyName,
			Func<string, Task>? loadFunc = null,
			bool suppressDuringSelection = false)
		{
			var clean = newValue ?? "";

			if (field == clean)
			{
				OnPropertyChanged(propertyName);
				return;
			}

			field = clean;
			OnPropertyChanged(propertyName);

			if (loadFunc == null)
				return;
		F
			if (suppressDuringSelection && _parent.IsSelectionLoadInProgress)
				return;

			//_ = loadFunc(clean);
			_ = LoadWithBusyAsync(loadFunc, clean);
		}
		*/

		public string SelectedCategory
		{
			get => _uiCategory;
			set
			{
				if (_suppressEditorSetters)
				{
					//Log.Debug($"SelectedCategory: suppressed setter, assigning backing field only");
					_uiCategory = value;
					OnPropertyChanged(nameof(SelectedCategory));
					return;
				}

				_uiCategory = value;
				OnPropertyChanged(nameof(SelectedCategory));

				if (!_parent._isCommittingShadow)
				{
					//Log.Debug($"SelectedCategory: shadow Category = {value}");
					_parent.MarkEditorDirty();

					_parent._shadow.Category = value;
					_parent._shadow.Counter = null;
					_parent._shadow.Instance = null;
					_ = _parent.ApplySelectedFromEditorAsync();
				}
			}
		}

		public string SelectedCounter
		{
			get => _uiCounter;
			set
			{
				// 1. If suppressed → assign only
				if (_suppressEditorSetters)
				{
					//Log.Debug($"SelectedCounter: suppressed setter, assigning backing field only");
					_uiCounter = value;
					OnPropertyChanged(nameof(SelectedCounter));
					return;
				}

				// 2. If loading → assign only
				if (_parent.IsLoading || _parent.IsSelectionLoadInProgress)
				{
					_uiCounter = value;
					OnPropertyChanged(nameof(SelectedCounter));
					return;
				}

				// 3. If unchanged → ignore
				if (_uiCounter == value)
					return;

				// 4. Normal user edit
				_uiCounter = value;
				OnPropertyChanged(nameof(SelectedCounter));

				//Log.Debug($"SelectedCounter: shadow Counter = {value}");
				_parent.MarkEditorDirty();

				_parent._shadow.Counter = value;
				_parent._shadow.Instance = null;
				_ = _parent.ApplySelectedFromEditorAsync();
			}
		}

		public string SelectedInstance
		{
			get => _uiInstance;
			set
			{
				if (_suppressEditorSetters)
				{
					//Log.Debug($"SelectedInstance: suppressed setter, assigning backing field only");
					_uiInstance = value;
					OnPropertyChanged(nameof(SelectedInstance));
					return;
				}

				if (_parent.IsLoading || _parent.IsSelectionLoadInProgress)
				{
					//Log.Debug($"SelectedInstance: uiInstance = {value}");
					_uiInstance = value;
					OnPropertyChanged(nameof(SelectedInstance));
					return;
				}

				if (!_parent._isCommittingShadow)
				{
					_parent.MarkEditorDirty();
				}
			}
		}

		/*
		public string SelectedCategory
		{
			get => _category;
			set => SetAndMaybeLoad(
				ref _category,
				value,
				nameof(SelectedCategory),
				LoadCountersAsync,
				suppressDuringSelection: true);
		}

		public string SelectedCounter
		{
			get => _counter;
			set => SetAndMaybeLoad(
				ref _counter,
				value,
				nameof(SelectedCounter),
				LoadInstancesAsync,
				suppressDuringSelection: true);
		}

		public string SelectedInstance
		{
			get => _instance;
			set => SetAndMaybeLoad(
				ref _instance,
				value,
				nameof(SelectedInstance));
		}

		private void SetCategorySilent(string value) => _category = value;
		private void SetCounterSilent(string value) => _counter = value;
		private void SetInstanceSilent(string value) => _instance = value;
		*/
		public void LoadFrom(CounterViewModel vm)
		{
			//Log.Debug($"[LoadFrom] vm.Category={vm.Category}, vm.Counter={vm.Counter}, vm.Instance={vm.Instance}");
			_loadingFromModel = true;
			try
			{
				Id = vm.Id;

				// Shadow only
				_parent._shadow.Category = vm.Category ?? "";
				_parent._shadow.Counter = vm.Counter ?? "";
				_parent._shadow.Instance = vm.Instance ?? "";
				/*
				SetCategorySilent(vm.Category);
				SetCounterSilent(vm.Counter);
				SetInstanceSilent(vm.Instance ?? "");
				*/

				DisplayName = vm.DisplayName;
				Min = vm.Min;
				Max = vm.Max;
				ShowInTray = vm.ShowInTray;
				IconSet = vm.IconSet;
				UseTextTrayIcon = vm.UseTextTrayIcon;
				TrayAccentColor = vm.TrayAccentColor;
				AutoTrayBackground = vm.AutoTrayBackground;
				TrayBackgroundColor = vm.TrayBackgroundColor;

				_parent.ResetEditorDirtyState();
			}
			finally
			{
				_loadingFromModel = false;
			}

			//_ = LoadCountersAsync(_category);
			//_ = LoadEverythingAsync();
		}

		/*
		private Task LoadEverythingAsync()
		{
			return LoadCountersAsync(_category);
		}

		public void LoadFrom(CounterViewModel vm)
		{
			_loadingFromModel = true;
			try
			{
				// Do NOT forget to copy GUID!
				Id = vm.Id;

				// Set the category — this triggers the full reload pipeline
				SelectedCategory = vm.Category;

				// After counters load, override the counter if needed
				SelectedCounter = vm.Counter;

				// After instances load, override the instance if needed
				SelectedInstance = vm.Instance ?? "";

				// Simple fields (no async, no dependencies)
				DisplayName = vm.DisplayName;
				Min = vm.Min;
				Max = vm.Max;
				ShowInTray = vm.ShowInTray;
				IconSet = vm.IconSet;
				UseTextTrayIcon = vm.UseTextTrayIcon;
				TrayAccentColor = vm.TrayAccentColor;
				AutoTrayBackground = vm.AutoTrayBackground;
				TrayBackgroundColor = vm.TrayBackgroundColor;

				_parent.ResetEditorDirtyState();
			}
			finally
			{
				_loadingFromModel = false;
			}
		}
		*/

		public void LoadDefaults()
		{
			var defaults = new DefaultSettingsProvider().CreateDefaultCounter();

			Id = Guid.NewGuid();

			// Set category — triggers full reload pipeline
			SelectedCategory = defaults.Category;

			// Override counter after counters load
			SelectedCounter = defaults.Counter;

			// Override instance after instances load
			SelectedInstance = defaults.Instance ?? "";

			// Simple fields
			DisplayName = defaults.DisplayName;
			Min = defaults.Min;
			Max = defaults.Max;
			IconSet = defaults.IconSet;
			ShowInTray = defaults.ShowInTray;
			UseTextTrayIcon = defaults.UseTextTrayIcon;
			TrayAccentColor = defaults.TrayAccentColor;
			AutoTrayBackground = defaults.AutoTrayBackground;
			TrayBackgroundColor = defaults.TrayBackgroundColor;

			_parent.ResetEditorDirtyState();
		}

		public bool UseTextTrayIcon
		{
			get => _useTextTrayIcon;
			set
			{
				if (_useTextTrayIcon == value) return;
				_useTextTrayIcon = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(ShowIconSetSelector));
				OnPropertyChanged(nameof(ShowBackgroundColorPicker));
			}
		}

		public System.Windows.Media.Color TrayAccentColor
		{
			get => _trayAccentColor;
			set
			{
				_trayAccentColor = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(TrayAccentBrush));
			}
		}

		public bool AutoTrayBackground
		{
			get => _autoTrayBackground;
			set
			{
				if (_autoTrayBackground == value) return;

				bool old = _autoTrayBackground;
				_autoTrayBackground = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(ShowBackgroundColorPicker));

				if (old == true && value == false && !_userHasPickedManualBackground)
				{
					TrayBackgroundColor = UIColors.GetTrayBackground(
						System.Drawing.Color.FromArgb(
							TrayAccentColor.A,
							TrayAccentColor.R,
							TrayAccentColor.G,
							TrayAccentColor.B),
						autoContrast: true
					).ToMediaColor();
				}
			}
		}

		public System.Windows.Media.Color TrayBackgroundColor
		{
			get => _trayBackgroundColor;
			set
			{
				_trayBackgroundColor = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(TrayBackgroundBrush));
			}
		}

		public bool ShowIconSetSelector =>
			ShowInTray && !UseTextTrayIcon;

		public bool ShowBackgroundColorPicker =>
			UseTextTrayIcon && !AutoTrayBackground;

		private void PickAccentColor()
		{
			TrayAccentColor = PickColor(TrayAccentColor);
		}

		private bool _userHasPickedManualBackground = false;

		private void PickBackgroundColor()
		{
			TrayBackgroundColor = PickColor(TrayBackgroundColor);
			_userHasPickedManualBackground = true;
		}

		private Color PickColor(Color initial)
		{
			var dlg = new System.Windows.Forms.ColorDialog
			{
				Color = System.Drawing.Color.FromArgb(initial.A, initial.R, initial.G, initial.B),
				CustomColors = _parent.GlobalSettings.Global.CustomColors.ToArray()
			};

			if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				// Save updated custom colors back into settings
				_parent.GlobalSettings.Global.CustomColors = dlg.CustomColors.ToArray();

				SettingsSaveQueue.Enqueue(SettingsMapper.ToDto(_parent.GlobalSettings));

				return Color.FromArgb(
					dlg.Color.A,
					dlg.Color.R,
					dlg.Color.G,
					dlg.Color.B);
			}

			return initial; // unchanged
		}

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
	}
}
