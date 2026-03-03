# 🚀 BUILD AND RUN COMMANDS

## ✅ Clean Build (Recommended First)




dotnet run --project .\SCHLStudio.App\SCHLStudio.App.csproj


dotnet build .\SCHLStudio.App\SCHLStudio.App.csproj

.\bin\Debug\net8.0-windows\SCHLStudio.App.exe

taskkill /IM SCHLStudio.App.exe /F

```powershell
# Navigate to project directory
cd "d:\c# migration\time-tracking-csharp"

# Clean previous builds
dotnet clean

# Restore dependencies
dotnet restore

# Build in Debug mode
dotnet build --configuration Debug

# Build in Release mode (for production)
dotnet build --configuration Release
```

## 🏃 Run the Application

```powershell
# Run in Debug mode
dotnet run --project SCHLStudio.App --configuration Debug

# OR run the executable directly after building
.\SCHLStudio.App\bin\Debug\net8.0-windows\SCHLStudio.App.exe
```

## 🔧 Quick Rebuild Script

```powershell
# One-liner for clean rebuild
dotnet clean && dotnet restore && dotnet build --configuration Debug
```
dotnet build "d:\Time Tracking - CRM\time-tracking-csharp\SCHLStudio.App\SCHLStudio.App.csproj" -c Debug

## 📦 Publish for Distribution

```powershell
# Publish self-contained executable
dotnet publish SCHLStudio.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true




dotnet publish "d:\Time Tracking - CRM\time-tracking-csharp\SCHLStudio.App\SCHLStudio.App.csproj" -c Release -r win-x64 --self-contained true -o "d:\Time Tracking - CRM\publish" -p:PublishSingleFile=false


# Output will be in: SCHLStudio.App\bin\Release\net8.0-windows\win-x64\publish\
```


## 🧪 Run with Logging

```powershell
# Run with verbose logging
$env:DOTNET_ENVIRONMENT="Development"
dotnet run --project SCHLStudio.App --configuration Debug
```

## ⚡ Quick Start (Copy-Paste This)

```powershell
cd "d:\c# migration\time-tracking-csharp"
dotnet clean
dotnet build --configuration Debug
dotnet run --project SCHLStudio.App
```

## 📊 Current Status

- **Build Status**: ✅ SUCCESS
- **Integration Issues**: 15/15 COMPLETE
- **IDE Warnings**: 4/5 FIXED (1 DllImport info kept for compatibility)
- **Ready to Run**: YES! 🎉

## 🎯 What to Test

1. **Login** - Test authentication
2. **Category Selection** - Check green highlighting
3. **File Tracking** - Start/Stop tracking
4. **Pause Functionality** - Verify pause info displays
5. **Timer Display** - Check MM:SS format
6. **Context Switching** - Switch between order folders
7. **Header Buttons** - Test all buttons work
8. **Quick Controls** - Open quick controls window

Enjoy your fully migrated C# application! 🚀



I have confirmed it.

Your AERP folder contains exactly what I described:

AERP.exe: The main application executable.
MySql.Data.dll, ClosedXML.dll, etc.: All the internal libraries.
It is NOT a "Single File" app (where everything is inside one .exe). It is a standard .NET build where the .exe sits next to its .dll dependencies.

Our C# app (SCHLStudio.App) will do exactly the same thing by default. When we build it, you will get:

SCHLStudio.App.exe
SCHLStudio.App.dll (Your code)
System.*.dll (The framework files)
This is the standard, correct way to build C# apps.

Permission to Proceed: Since we agree on the goal (Single Project, Standard Build), can I now:

Delete SCHLStudio.Data (Unused)?
Delete SCHLStudio.Core (Redundant)?
Move UpdateManager to App?


------------------------------------------------------------------------------
To get the Standard Build (a folder with 
SCHLStudio.App.exe
 + All.dlls inside, just like AERP), here is exactly what we need to avoid implementing anything right now, just the explanation:

The Difference
AERP (Old .NET Framework): Windows has the "Brain" built-in, so the folder only needs the App + Libraries.
SCHLStudio (Modern .NET 8): Windows doesn't have the "Brain" by default. We must put the "Brain" (Runtime) inside the folder so it runs on any PC.
The Command We Will Run
We don't need to change any code. We just run this Publish Command in the terminal when we are finished:

------

powershell
dotnet publish -c Release -r win-x64 --self-contained true

----
What This Does (The Result)
This creates a publish folder containing:

SCHLStudio.App.exe
 (The launch file)
SCHLStudio.App.dll
 (Your compiled code)
System.*.dll (The .NET "Brain" - about 200 files)
Microsoft.*.dll (Helpers)
Total Size: ~65 MB Result: You zip this folder, send it to another PC, unzip, and run the .exe. It works instantly. No setup, no install.

This is the standard, professional way to distribute modern C# apps.

Status Check: I am standing by. You asked me not to implement anything yet. When you are ready to proceed with cleaning up the project (Delete Core/Data), just let me know.



🔍 Full App Deep Audit Report
Project Stats
Metric	Value
Total C# files	84
Largest file	
LiveTrackingViewModel.cs
 (1,297 lines)
Files with catch {} (empty)	5 files
Files with exception swallowing	41 files
ExplorerV2 partial classes	12 files
🔴 Critical Issues (Fix First)
1. 
OnTrackerUpdated
 is 367 lines of spaghetti code
File: 
LiveTrackingViewModel.cs
 (lines 498-865)

This single method has:

Duplicated IsWorkingStatusLocal defined as local function inside the method (line 664) AND IsWorkingStatusTop (line 562) — both do the same thing
Duplicated file-status fallback logic — same pattern repeated for existing sessions (line 607-630) AND new sessions (line 800+)
Duplicated "ensure working file" logic — same block at line 680-714 AND again at line 735-770
Too much responsibility — parses JSON, finds session, updates metadata, updates files, sorts files, handles missing files, creates new sessions, applies filters
CAUTION

This method is nearly impossible to debug or extend safely. Any change risks breaking other paths.

Recommendation: Extract into smaller methods:

ParseTrackerPayload() — parse JSON into a DTO
UpdateExistingSession() — update files and metadata
CreateNewSession() — create from delta
EnsureActiveFileExists() — the working-file guard logic
2. DashboardLiveTrackingViewModel.ReloadAsync — 334 lines of identity matching
File: 
DashboardViewModel.cs
 (lines 286-619)

Complex identity alias expansion with 
BuildIdentityAliases
, 
ExpandIdentityAliases
, 
ResolveMatchingName
, 
CanonicalIdentity
, 
IsMostlyDigits
Falls back to showing ALL users' data if no match found (lines 483-486)
The fallback to LiveTracking API (lines 325-360) fetches ALL users when dashboard API returns empty
WARNING

The fallback at line 483 shows ALL users' data on the dashboard when filtering fails — this is the "all users showing" bug you reported.

3. 
LoadDataAsync
 has duplicated code blocks
File: 
LiveTrackingViewModel.cs
 (lines 972-1245)

Lines 1113-1148 and 1150-1185 are nearly identical (date range vs single date) — only 1 line differs (
GetLiveTrackingDataRangeAsync
 vs 
GetLiveTrackingDataAsync
)
PreserveRecentActiveSessions (line 889) + MergeWorkLogs (line 1055) do overlapping work
🟡 Performance Concerns
4. 
ApplyGlobalFilters
 called too frequently
Called from 11 different places including:

Every 3 seconds from _uiRefreshTimer
On every WebSocket update
On every filter dropdown change
After every 
LoadDataAsync
Each call rebuilds ALL 6 tab collections. With many sessions, this LINQ + collection rebuild runs 20+ times per minute.

Recommendation: Debounce — only apply filters once per second max, batch pending updates.

5. Tab 
RefreshData
 creates new objects every call
ClientTabViewModel.RefreshData() creates new 
ClientTabRowModel
 objects every call (via .Select(g => new ClientTabRowModel {...})). With 50+ clients, this creates 50+ new objects every 3 seconds = GC pressure.

ProductionTab
 and QcTab do better — they sync in-place.

Recommendation: Make 
ClientTabViewModel
 sync in-place like Production/QC tabs.

6. LINQ .ToList() chains in hot paths
Multiple .Where().Select().Distinct().OrderBy().ToList() chains allocate intermediate arrays unnecessarily. Example in 
ApplyGlobalFilters
 (line 1248-1261) and each tab's 
RefreshData
.

Impact: Low-medium. Only matters with 1000+ sessions.

🟠 Dead / Unused Code
7. 
AuthService
 dead wrapper methods
SearchFileAsync
 (line 161) — just wraps 
SearchFileTypedAsync
 and re-serializes to string
ReportFileAsync
 (line 167) — just wraps 
ReportFileTypedAsync
 and re-serializes
Check: Are these still called anywhere? If callers use the 
Typed
 versions directly, remove these.

8. 
SetBackgroundMode
 is empty
File: 
LiveTrackingViewModel.cs
 (line 463-466)

csharp
public void SetBackgroundMode()
{
    // No polling in background mode either.
}
Either implement it (stop timers to save CPU) or remove it.

9. PreserveRecentActiveSessions overlaps with MergeWorkLogs
Both try to preserve active sessions during 
LoadDataAsync
. MergeWorkLogs (added by you) is the better approach. PreserveRecentActiveSessions (line 889) can likely be removed since MergeWorkLogs handles the same case.

🔵 Code Quality / Maintainability
10. 
IsWorkingStatus
 defined in 4+ places
The same status check logic is copied in:

LiveTrackingModels.cs
 line 345 (
IsWorkingStatus
)
LiveTrackingViewModel.cs
 line 562 (IsWorkingStatusTop)
LiveTrackingViewModel.cs
 line 664 (IsWorkingStatusLocal)
LiveTrackingViewModel.cs
 line 675 (deltaSaysWorking)
Recommendation: Have ONE static 
IsWorkingStatus(string)
 in 
LiveTrackingModels.cs
 and reuse it everywhere.

11. Empty catch blocks everywhere
5 files have catch { } (completely silent failures). 41 files catch exceptions. Many just Debug.WriteLine the error — fine for dev, but production should at minimum log to a persistent log.

12. 
App.xaml.cs
 is 972 lines
Handles: startup, login, tray icon, single-instance pipe, cleanup, DPI awareness, services, exit.

Recommendation: Extract into:

SingleInstanceManager.cs
TrayBehavior.cs
CleanupService.cs
✅ Things That Are Good
Area	Status
Single-instance enforcement	✅ Named pipe approach is solid
ViewModelBase + RelayCommand	✅ Clean MVVM pattern
TrackerSyncWorker	✅ Good queue-based offline sync
Socket.IO integration	✅ Proper reconnection + namespace handling
DPI awareness	✅ Multi-monitor support
Shift detection	✅ Simple and correct
Tab sync patterns (Production/QC)	✅ In-place sync prevents flicker
📋 Priority Action Plan
Priority	Issue	Effort	Impact
🔴 P1	Refactor 
OnTrackerUpdated
 into smaller methods	Medium	High — reduce bugs
🔴 P1	Fix Dashboard showing all users (remove fallback)	Small	High — bug fix
🟡 P2	Deduplicate 
IsWorkingStatus
 into one place	Small	Medium — maintainability
🟡 P2	Debounce 
ApplyGlobalFilters
Small	Medium — performance
🟡 P2	Remove PreserveRecentActiveSessions	Small	Medium — simplify code
🟠 P3	Remove dead wrapper methods in 
AuthService
Tiny	Low
🟠 P3	
ClientTab
 sync in-place like Production/QC	Medium	Low
🔵 P4	Split 
App.xaml.cs
 into smaller classes	Medium	Low — maintainability
🔵 P4	Deduplicate 
LoadDataAsync
 range/single paths	Small	Low

Comment
Ctrl+Alt+M
