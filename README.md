
Performance Tray Monitor

	A lightweight, animated performance monitor for the Windows system tray.

# Description:

	On startup an animated taskbar icon is available. 
	Left clicking this icon will show a menu. 
	Right clicking will show the defined performance metrics.

	The icon menu has:
		Configur Metrics:			To Add/Edit/Remove counters
		Open/Close Icon Preview:	Shows the found icon sets (mostly for debugging icon issues).
		Open/Close Metrics View:	Display all defnited counters in a windows, this window automatically updates all counters.
		Hide/Show App icon:			If any counter tray icons are active you can hide the app icon, this will persist over restarts!
									To get the app icon back left-click on any counter tray icon!
		Exit:						Save setttings and exit the app
		About:						About

	On startup, when no settings file exists a default Disk Activity metric is defined (but no taskbar icon is shown).

# Building

	For building, I use: Microsoft Visual Studio Community 2022/2026 edition (latest)

	## Requirements:

		- Windows 10/11.
		- Visual Studio 2022/2026 Commuinity Edition

		- For debugging Required packages:
			- Microsoft.Extensions.Logging
			- Microsoft.Extensions.Logging.Debug
			- Microsoft.Extensions.Logging.TraceSource
		
			To install these packages, use: Nuget commands (Tools > NuGet Package Manager > Package Manager Console):
				- Install-Package Microsoft.Extensions.Logging
				- Install-Package Microsoft.Extensions.Logging.Debug
				- Install-Package Microsoft.Extensions.Logging.TraceSource

	Build the app.

# Download:
	<a href="https://https://github.com/JohanD-H/PerformanceTrayMonitor/releases/latest">
		<img src="PerformanceTrayMonitor.png" alt="Download Now" width="48" style="display:block; margin-left:0;" />
	</a>

# Installation:

	Unpack the <release>.zip file into a subdirectory in your root C:\, %USERPROFILE%, or %APPDATA%.
	Do NOT install the app into %ProgramFiles% or %ProgramFiles(x86)%! 
	The app saves it'ssettings in PerformanceTrayMonitor.json, using %ProgramFiles% or %ProgramFiles(x86)% would cause a write protection error!

# Help / FAQ

	**Where is the app window?**  
		It’s designed to live in the **tray**. Use the tray icon context menu.

	**Can it start with Windows?** 
		Yes.

# Code Structure...

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


# Why are there two ConfigViewModels?

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

# Metrics config

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

# IconSet(s):
	
	You can install addition (custom) .ico files into a <app directory>\Icons\<subdirectory name, a.k.a IconSet name.
	The requirements are, at least two .ico files are required, the filename part must end on -1 for the first file, -2 for the seconds, etc. upto -10.
	The maximun number of .ico files is 10.
	The formula for determining which .ico file to use: (max - min) * ((max / (n - 1) ) / 100) * (n - 1) + min
		Where:	n = the .ico number (1, 2,...10)
				min and max are the lower and upper value limits defined in the metric configuration!
	A demonstration IconSet is defined into the Icons directory named Thumb.

# .NET:

	Debugging commands:
		List .NET runtimes:
			dotnet --list-runtimes
		Print .NET (silent) exceptions (for PerformanceTrayMonitor in this case):
			dotnet .\PerformanceTrayMonitor.dll
# Changes:

	v1.0.9564	Initial release
	v1.0.9565	Fixed wrong UI URI, set to pack URI!
	v1.1.9567	Store Metric view windows position in .json (with all the bagage that comes with that). 
				Was not happy with the UI logic, totally rewrote that code.
	v1.1.9568	Use LoadDefaults() in ResetToDefaults(), no duplication of efford.
				Code cleanup
				The Metric View window was starting too early when pinned, plus it needed some massaging
	v1.2.9569	Fix a bunch of UI issues, added Icon preview
	v1.3.9575	Completely seperated loading Windows Performance Counters from the UI, this makes
				the UI responsive under all condition. Also added a UI Updating overlay, 
				added a status bar to the Configuration UI. Changed Tray Icon menu to use the
				WPF context menu, added a tiny (WPF) context menu to the counter icons (only
				shown when the app tray icon is hidden). Rewrote settings saving to be async,
				and use json, as json was always intended to be used for this (instead of using
				xml), save some more settings and seperated metric from global settings, so
				they can be saved independently.
	v1.3.9585	Added a small Metric graph, accessible from the Metrics view, and clicking the metric name.
				Fixed a lot of UI inconsitencies.
	v1.3.9586	Graphs using sparkline, could draw outside their window limits, fixed.
				Icon set preview in the Metric configuration would not start updating due
				to the icon frames not being setup. Added the loading into metric config startup.
				Combined duplicate code, remove old code.


More to come ...
