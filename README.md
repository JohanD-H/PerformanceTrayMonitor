
Performance Tray Monitor

	A lightweight, animated performance monitor for the Windows system tray.

Description:

	On startup an animated taskbar icon is available. 
	Left clicking this icon will show a menu. 
	Right clicking will show the defined performance metrics.

	The icon menu has:
	Configuration:	To Add/Edit/Remove counters
	Show frames:	Shows the found icon sets (mostly for debugging icon issues).
	Show counters:	Display all defnited counters in a windows, this window automatically updates all counters.
	Hide App icon:	If any counter tray icons are active you can hide the app icon, this will persist over restarts!
					To get the app icon back left-click on any counter tray icon!
	Exit:			Save setttings and exit the app
	About:			About.

	On startup, when no settings file exists a default Disk Activity metric is defined (but no taskbar icon is shown).

Building

	For building, use: Microsoft Visual Studio Community 2022 edition (latest)

		Required packages:
			Microsoft.Extensions.Logging
			Microsoft.Extensions.Logging.Debug
			Microsoft.Extensions.Logging.TraceSource
		To install use: Nuget commands (Tools > NuGet Package Manager > Package Manager Console):
			Install-Package Microsoft.Extensions.Logging
			Install-Package Microsoft.Extensions.Logging.Debug
			Install-Package Microsoft.Extensions.Logging.TraceSource

Code Structure...

	App.xaml & App.xaml.cs
		Main entry
		Main exit

	BaseViewModel.cs
		Does nothing right now

	MainViewModel.cs
		This is the source of truth.
		Holds live counters.
		Holds live settings.
		Holds the shared ConfigViewModel.
		Rebuilds tray icons when settings change.
		Creates a temporary ConfigViewModel for the config window.

	CounterTrayIcon.cs
		This is the primary UI when the app icon is hidden.
		Shows animated frames based on counter value.
		Left‑click → opens popup.
		Right‑click → restores the app icon if hidden.

	AnimatedTrayIcon.cs
		This is the app icon “control center”.
		Provides the menu: Config, Exit, About, Show/Hide App Icon.
		Provides access to popup.
		Animates independently.
		Can be hidden entirely.
	
	TrayIconManager.cs
		This is the tray icon orchestrator.
		Creates the app icon (only if ShowAppIcon = true).
		Creates counter icons (Unto MaxCounterTrayIcons -> TrayIconConfig.cs).
		Rebuilds icons when settings change.
		Recreates the app icon when needed (e.g., after user restores the app icon).


Why are there two ConfigViewModels?

	Shared ConfigViewModel (SharedConfigVm)
		Lives for the entire lifetime of the app.
		Is not the editor VM.
		
		Provides:
			Icon set lists
			Global settings
			Commands (Reset, Save, etc.)
		Is used by:
			AnimatedTrayIcon
			TrayIconManager
			CounterTrayIcon (indirectly through manager)

	Temporary ConfigViewModel (freshVm)
		Created when the user opens the config window.
		Used only for editing.
		Discarded after saving or canceling.
		Does not affect tray icons until saved.

Metrics config

	ConfigViewModel.cs
		It is the metrics editor VM, not the shared VM!
		It maintains its own collection of CounterViewModels
		Ensures:
			Cancel works
			Reset works
			Edits don’t affect live tray icons
			The user can discard changes safely
		Uses a CounterEditorViewModel for field‑level editing
			isolates:
				all metric selections
	
	CounterEditorViewModel.cs
		Editor for a single counter
		Owns its own fields
		UsesConfigViewModel for loading category/counter/instance

	CounterViewModel.cs
		represents a live counter instance
		bridge between MainViewModel and CounterTrayIcon
			CounterTrayIcon never touches PerformanceCounter directly
			It only reads CurrentValue
			CounterViewModel controls the update rate
			CounterTrayIcon controls the animation

	DebugViewModel.cs
		Build dynamic window
		Shows IconSets and validates them
		Icon testing and validation tool


.NET:

	Debugging commands:
		List .NET runtimes:
			dotnet --list-runtimes
		Print .NET (silent) exceptions (for PerformanceTrayMonitor in this case):
			dotnet .\PerformanceTrayMonitor.dll

More to come ...


