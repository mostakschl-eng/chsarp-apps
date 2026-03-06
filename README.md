# SCHL Studio V2

A modern, high-performance time tracking and file management application for production studios.

## Project Structure

- **SCHLStudio.App**: WPF User Interface (MVVM)
- **SCHLStudio.Core**: Business Logic, Tracking Engine, and Services
- **SCHLStudio.Data**: Data Access and Local Storage

## Setup

1. Open `SCHLStudio.sln` in Visual Studio 2022.
2. Restore NuGet packages.
3. Build and Run.



Live Tracking Data & Calculation Report
This report explains exactly how the C# Live Tracking dashboard calculates every single metric across all its tabs based on the NestJS backend data.

1. Backend Data Sources
The entire Live Tracking dashboard is powered by the GET /tracker/live-tracking-data endpoint. It queries two MongoDB collections:

qc_work_logs: Contains the actual files worked on, the exact time spent, ET limits, and pause reasons.
user_sessions: Tracks exactly when an employee opened the app (login_at) and closed it (logout_at), tracking their active presence even if they aren't working on a file.
2. Client Tab Calculation Logic
Path: 
ClientTabViewModel.cs
 The Client Tab groups all active reading sessions by ClientCode.

A. Top Cards
Active Clients: Count of unique ClientCode strings where IsActive == true.
Total Employees: Count of unique EmployeeName currently working inside those active client folders.
Files Completed: Count of unique file paths where file.FileStatus == "done".
Total Time Spent: The sum of EffectiveTimeSpent across all files in all sessions.
B. Client Grid Columns
Client Name: Taken straight from client_code.
Active Employees: Count of unique employees currently inside this client's jobs.
Categories: Extracts unique strings from the Categories field of the active jobs.
Completed Production Files: Counts files marked as "done" where the job's 
WorkType
 does not start with "QC".
Completed QC Files: Counts files marked as "done" where the job's 
WorkType
 does start with "QC".
Estimate Time (ET): Adds together the highest EstimateTime of each unique Folder/WorkType combination under this client.
Total Time Spent: The exact sum of time spent on all files for this client.
Avg Time: Total Time Spent $\div$ 
(Completed Production Files + Completed QC Files)
. If 0 files are completed, it shows "—" to prevent division errors.
Start Time: The earliest CreatedAt timestamp among all files for this client today.
End Time: The latest UpdatedAt timestamp among all files.
3. Production Tab Calculation Logic
Path: 
ProductionTabViewModel.cs
 This tab filters the raw data to strictly show jobs where 
WorkType
 does not contain the word "QC". It groups by the individual Job Session.

A. Top Cards
Active Users: Unique employee names currently active in Production jobs.
Total Files: Total count of files locked/imported into Production jobs today.
Completed Files: Files strictly marked as FileStatus == "done".
Avg Time Per File: Total Time across all production files $\div$ Total Files.
B. Production Grid Columns
Employee Name: The user working on the job.
Client: The client_code.
Work Type: The work_type string.
Shift: The user's shift.
Progress: Formatted as [Completed Files] / [Total Files inside this specific folder].
Current File: The name of the file currently marked "working". If nothing is working, it falls back to the exact last file imported.
ET: The specific estimate_time limit set for this specific job.
Avg Time: Total time spent on this folder $\div$ (Files marked "done" + Files marked "walkout").
Warning Trigger: If this calculated Avg Time exceeds the ET, the row alerts the user (changes color/flags it).
Total Time: Master sum of time for this job folder.
4. QC Tab Calculation Logic
This is structurally identical to the Production Tab, except its primary filter ensures that it only shows jobs where the 
WorkType
 string contains "QC".

5. User Summary Tab Calculation Logic
Path: 
UserSummaryTabViewModel.cs
 This tab is the most complex. It merges the physical file work (qc_work_logs) with the user's raw computer presence (user_sessions).

A. Grid Columns
Employee Name: The user's name. It takes the text before the hyphen if formatted like "A123 - John Doe" to match them up securely.
Status: If the user has an open user_sessions with logout_at == null, it shows Active. If they closed the app, it shows Logout.
Total Work Time: The sum of EffectiveTimeSpent for all files they touched today.
Total Pause Time: The sum of duration across all their PauseReasons today.
Total Files: Count of files they marked "done".
First Login: The absolute earliest login_at timestamp in their session history today.
Last Logout: The absolute highest logout_at timestamp. (If they are currently Active, this displays as "—").
Total Shift Time (Total Duration Today): The exact span between their First Login and Last Logout (or Current Time if Active), clamped strictly to a 24-hour boundary if they worked past midnight.
Idle Time: The master calculation: Total Shift Time $-$ (Total Work Time $+$ Total Pause Time). If they have the app open but are not touching a file and not officially paused, it counts as Idle.
6. Real-Time Math ("Effective Time")
Across all tabs, time isn't just a static DB number. Because the dashboard is "Live", it uses a property called EffectiveTimeSpent. If a Backend file is marked "working", the C# app uses your PC clock to do this: EffectiveTimeSpent = Math.Max(Database Time Spent, (Current UI Time - Backend started_at timestamp)) This guarantees the stopwatch on the live dashboard keeps ticking up smoothly every second without spamming the backend database.




{
  "_id": {
    "$oid": "69aa905f7dbedef6b3c8a684"
  },
  "shift": "morning",
  "work_type": "production",
  "client_code": "0073_dd",
  "folder_path": "P:\\SCHL Production\\0073_DD\\MARCH 2026\\05 Mar 26\\Retouching_20260305-CAT_3pics",
  "employee_name": "0026 - robiul islam",
  "date_today": "2026-03-06",
  "__v": 0,
  "categories": "Retouch",
  "createdAt": {
    "$date": "2026-03-06T08:29:19.525Z"
  },
  "estimate_time": 10,
  "files": [
    {
      "file_name": "S00291_AP_GP09_77605OK.OO.A517CA.01_ET1",
      "file_status": "working",
      "report": "",
      "time_spent": 65,
      "started_at": {
        "$date": "2026-03-06T08:29:19.552Z"
      }
    }
  ],
  "pause_count": 1,
  "pause_reasons": [
    {
      "reason": "auto pause",
      "duration": 1,
      "started_at": {
        "$date": "2026-03-06T08:30:25.257Z"
      },
      "completed_at": {
        "$date": "2026-03-06T08:30:25.874Z"
      }
    }
  ],
  "pause_time": 1,
  "processed_sync_ids": [
    "23d8c30447e848a1ad2b5f8df0fb744d",
    "7b958df57cf1428388603fae50fb1c6c",
    "978357fe83464d57b6e0457d44ef6006",
    "6efa1df9b1d94cc79f9336e00ff34ea7"
  ],
  "total_times": 65,
  "updatedAt": {
    "$date": "2026-03-06T08:30:25.891Z"
  }
}