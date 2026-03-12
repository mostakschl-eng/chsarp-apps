# 🚀 BUILD AND RUN COMMANDS

## ✅ Clean Build (Recommended First)

dotnet build SCHLStudio.App\SCHLStudio.App.csproj


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


Looking at your LiveTrackingViewModel, here's what I see:

🔴 Real Issues
1. _ = LoadDataAsync() called multiple times redundantly
In ApplySingleDateCommand, you call SelectedDate = date.Date which already triggers LoadDataAsync() via the setter — then you call _ = LoadDataAsync() again right after. Double load on every date change.
2. _loadLock.Wait(0) silently skips loads
If a load is in progress and the timer fires, it just skips. Fine for the polling timer, but if the user changes a filter while loading, they get stale data with no feedback. No retry, no queue.
3. Empty catch {} blocks in the UI timer
csharp_uiRefreshTimer.Tick += (s, e) =>
{
    try { try { ... } catch { } }
    catch { }
};
Nested try/catch swallowing everything silently. If NotifyLiveTick() or ApplyGlobalFilters() throws, you'll never know.
4. Debug.WriteLine($"... {ex.Message}") in LoadDataAsync
You already know this one — loses the stack trace. Should be {ex}.

🟡 Real-time Specific Concerns
5. OnTrackerUpdated does linear search every update
csharpvar session = _allData.FirstOrDefault(s => ...)
If you have 50+ users with frequent socket updates, this scans the full list every tick. For a real-time dashboard this adds up. A Dictionary keyed by employee+shift+folder would be faster.
6. IsWorkingStatusTop defined twice as local functions
Same function defined inside OnTrackerUpdated as both IsWorkingStatusTop and IsWorkingStatusLocal — they do the exact same thing. One private method on the class is enough.
7. Socket handlers don't check if the selected date matches today
If user is viewing yesterday's data, socket updates for today still mutate _allData and call ApplyGlobalFilters() — so yesterday's view can get polluted with today's live events.

🟢 What's Actually Good

Semaphore on LoadDataAsync — correct
ShouldReloadLiveSnapshot() before polling — smart
Preserving previous data when snapshot returns 0 results — protects against flicker
Restoring filter selections after reload — good UX detail
MergeWorkLogs comparing UpdatedAt before replacing — solid

plan -


Clean Implementation Plan: Working Files Resumption
This plan outlines the exact changes needed to allow the C# app to resume "working" files across sessions seamlessly. It covers adding the specific file_path property to the backend schema and the time-calculation logic to recover lost time.

1. Backend Changes (NestJS)
A. Add file_path to Database Schema
The C# app needs to know exactly where individual files are located (e.g., if files came from different sub-folders inside the main job folder).

File: 
schl-web/packages/common/src/models/qc-work-log.schema.ts
Action: Add a file_path property string to the @Schema() export class QcWorkLogFile object.
Action: Ensure the SyncQcWorkLogDto (the incoming request model) also accepts file_path so the C# app can send it.
B. Disable the "Force Walkout" Auto-Cancel
Currently, if a user logs in or out, the backend finds their old "working" files and forcefully changes them to "walkout". This prevents resumption.

File: 
schl-web/apps/schl-api/src/modules/tracker/tracker.auth.service.ts
Action: In 
login()
, remove or disable the updateMany command that sets stuck working files to walkout.
Action: In 
logout()
, remove the similar updateMany command. Keep the files as "working".
C. Create an "Active Session" API Endpoint
When the C# app starts up, it needs an endpoint to ask: "Does this user have any working files right now?"

File: 
tracker.controller.ts
 & 
tracker.qc-work-log.service.ts
Action: Add a GET /tracker/active-work-log endpoint.
Action: It queries qc_work_logs for today's date and the current user's name where files.file_status == 'working'.
Payload: It will return the job's Metadata (client_code, folder_path, shift, work_type), and an array of the exact files including their file_name, file_status, started_at, and time_spent.
2. Frontend Changes (C# WPF App)
A. Update Sync Logic to include file_path
When the employee starts a file, the C# app must send not just the file name, but its path.

File: SCHLStudio.App/Services/Api/Tracker/SyncWorkLogDto.cs
Action: Add 
FilePath
 to the SyncWorkLogFileDto. Ensure ExplorerV2ViewModel populates this when making the sync call.
B. Fetch Active Session on Login
Immediately after a successful login, the app must check if there is unfinished work.

File: SCHLStudio.App/ViewModels/ExplorerV2/ExplorerV2ViewModel.cs (or the authentication handler).
Action: Call GET /tracker/active-work-log.
If data is returned, transition the UI into "Locked Working Mode".
C. Lock the UI and Reload Metadata
Action: Navigate the Explorer TreeView to the exact folder_path returned by the server.
Action: Fill out the metadata fields (client_code, work_type, shift) using the server data.
Action: Select the exact files in the grid that match the file_name and file_path array returned from the server.
Action: Lock the Explorer tree and inputs. The user cannot select a new folder or change metadata. They are locked into this active job.
D. Time Calculation Logic (Clock-Based Resumption)
Because we are relying on clock time, we don't just rely on the database's time_spent integer (which might be stale). Instead, we calculate missing time using the started_at timestamp.

Action: For each working file, take the server's started_at UTC timestamp.
Calculation: Current UI Time = (Current UTC Time - started_at) + previously saved time_spent.
Example scenario: The database says you started at 10:00 AM. Your PC crashed. You open the app at 10:15 AM.
10:15 AM - 10:00 AM = 15 minutes.
The UI timer instantly displays 15:00 minutes and starts counting up from there.
Action: The C# "Pulse" timer loop (which usually ticks up by 1 second) continues syncing this newly calculated master time down to the server.
3. Review and Next Steps
This plan covers your exact requirements:

Adding file_path so we know the exact source of every individual file lock.
Checking for old jobs upon login.
Loading metadata and locking the UI to that specific job.
Correctly calculating the lost time using started_at and Current Time.
If you are satisfied with this architecture, we can begin implementing it layer by layer, starting with the NestJS schema changes.

