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
user_sessions: Tracks exactly when an employee opened the app (login_at) and closed it (logout_at), tracking their active presence even if they aren't working on a file. 2. Client Tab Calculation Logic
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
End Time: The latest UpdatedAt timestamp among all files. 3. Production Tab Calculation Logic
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
Total Time: Master sum of time for this job folder. 4. QC Tab Calculation Logic
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
Idle Time: The master calculation: Total Shift Time $-$ (Total Work Time $+$ Total Pause Time). If they have the app open but are not touching a file and not officially paused, it counts as Idle. 6. Real-Time Math ("Effective Time")
Across all tabs, time isn't just a static DB number. Because the dashboard is "Live", it uses a property called EffectiveTimeSpent. If a Backend file is marked "working", the C# app uses your PC clock to do this: EffectiveTimeSpent = Math.Max(Database Time Spent, (Current UI Time - Backend started_at timestamp)) This guarantees the stopwatch on the live dashboard keeps ticking up smoothly every second without spamming the backend database.

{
"\_id": {
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

Exact Live Tracking Calculation Logic Plan
This plan documents exactly how every single field across all 6 tabs will calculate its data, directly incorporating your recent feedback.

1. Client Tab
   Goal: Show exactly what is happening under each client right now, calculating actual wall-clock elapsed time from first start to current time, and counting completed files without duplicates.

Top Cards:

Active Clients: Count of unique client_code strings where at least one user is currently working.
Total Employees: Count of unique employee_name currently working across all clients.
Files Completed: Count of unique files[].file_name (or folder_path\file_name) where files[].file_status is "done" for this client today. Total files completed today regardless of dupes (if a file is done twice, it counts as 1).
Total Time Spent: The exact wall-clock span of (End Time - Start Time) for all active clients today.
Grid Data (Per Client Row):

Client Name: The name of the client (client_code).
Active Employees: Count of unique employee_name currently working inside this client's folder.
Categories: The comma-separated categories string for this client.
Completed Production Files: Distinct count of unique files marked "done" where work_type does not contain "qc".
Completed QC Files: Distinct count of unique files marked "done" where work_type does contain "qc".
Estimate Time (ET): The max estimate_time limit set for the job groups under this client.
Start Time: The absolute earliest (minimum) createdAt recorded when the first user started working on this client today.
End Time: If any employee is currently working on this client (has a file with file_status == "working"), this is the Current Clock Time. If no one is working, this is the absolute latest (maximum) updatedAt recorded.
Total Time Spent: Strictly
(End Time - Start Time)
. This gives the exact span of how long work has been happening on this client (e.g., 10:10 to 10:30 = 20 minutes).
Avg Time: Total Time Spent /
(Completed Production Files + Completed QC Files)
. If 0 files completed, it shows "—". 2. Production Tab
Goal: Show active, non-QC jobs. All data here strictly excludes any job where
WorkType
contains "qc".

Top Cards:

Active Users: Count of unique EmployeeName currently working in Production.
Total Files: Must match the exact deduplicated count of files in the jobs. If "Total Completed" is 20, "Total Files" must clearly reflect the actual base files available so math aligns perfectly.
Completed Files: Distinct count of unique files marked "done" across Production today.
Avg Time Per File: Sum of all Total Time Spent on Production jobs / Total Files.
Grid Data (Per Employee/Job Session Row):

Employee Name: The user working (employee_name).
Client: client_code.
Work Type: work_type.
Shift: shift.
Progress: [Count of files[] where file_status == "done"] / [Total length of files[] array].
Current File: The files[].file_name currently marked file_status == "working".
ET: The estimate_time limit for this job.
Total Time: Strictly
(End Time - Start Time)
for this specific job session (using updatedAt - createdAt). If working, End Time is the Current Clock Time.
Avg Time: Total Time /
(Total Files array length - Count of files[] where file_status == "skip")
. 3. QC Tab
Goal: Show active QC jobs. All data here strictly requires that
WorkType
contains "qc".

Top Cards:

Active Users: Count of unique EmployeeName currently working in QC.
Total Files: Total unique files imported/locked into active QC jobs today.
Completed Files: Distinct count of unique files marked "done" across QC today (Duplicates ignored).
Avg Time Per File: Sum of all Total Time Spent on QC jobs / Total Files.
Grid Data (Per Employee/Job Session Row):

Employee Name: The user working.
Client: ClientCode.
Work Type:
WorkType
(e.g., QC1, QC2).
Shift: Shift.
Progress: [Files marked "done" in this specific folder] / [Total Files in this specific folder].
Current File: The FileName currently marked "working".
ET: The EstimateTime limit for this job.
Total Time: Strictly
(End Time - Start Time)
for this specific job session. If working, End Time is the Current Clock Time.
Avg Time: Total Time /
(Total Files - Skipped Files)
. 4. User Summary Tab
Goal: Track the overall daily performance of each employee.

Grid Data (Per Employee Row):

Employee Name: The uniquely parsed name of the employee (e.g., "0026 - robiul islam").
Total Work Time: Exact wall-clock span of all work done today. Calculated by finding the earliest createdAt and latest updatedAt (or current clock if active) for every job they touched today and summing those spans. If the user is active, we also add the real-time "Effective Time" which is Math.Max(time_spent, Current UI Time - files[].started_at).
Total Pause Time: The exact sum of all pause durations recorded for this employee today.
Total Files: Distinct count of unique files marked "done" by this employee today.
First Login: The absolute earliest login time recorded for them today.
Last Logout: The absolute latest logout time recorded for them today (or "—" if Active).
Total Duration Today: Last Logout (or Current Time if active) - First Login. Clamped to midnight to prevent cross-day errors.
Idle Time: Max(0, Total Duration Today - (Total Work Time + Total Pause Time)). Defines time spent logged in but not working or paused.
Status: "Active" if currently logged in, "Logout" otherwise. 5. Productivity Tab
Goal: Group employees by Categories to see output metrics.

Top Cards:

Total Files: Absolute total files imported in the system.
Total Users: Count of unique employees who have worked today.
Completed Files: Distinct count of unique files marked "done" across the whole system today.
Avg Time Per File: Sum of all Total Time Spent globally / Completed Files.
Avg Files Per User: Completed Files / Total Users.
Grid Data (Grouped by Category -> Employee):

Employee Name: The user.
Work Type: The comma-separated work types they performed under this category.
Total Files: Count of files they handled in this category.
Completed Files: Distinct count of "done" files they processed in this category.
Total Time: The exact wall-clock span (End Time - Start Time) they spent working in this category.
Avg Time: Total Time / Completed Files. 6. Pause & Activity Tab
Goal: Monitor live pause statuses and total daily pause breaks.

Top Cards:

Total Paused Users: Count of users currently matching a "pause" or "break" status.
Total Working Users: Count of users currently matching a "working" status.
Total Pause Time: Sum of all pause durations for all users today.
Total Working Time: Sum of all precise wall-clock work spans for all users today.
Avg Pause Time: Total Pause Time / Total Paused Users.
Top Pause Reason: The most frequently occurring string reason across all pauses today.
Grid Data (Per Employee Row):

Employee Name: The user (employee_name).
Total Work Time: Exact wall-clock span of all work done today
(Sum of End Time - Start Time across their jobs)
. (Just like the User Summary).
Total Pause Time: Sum of all pause_reasons[].duration today (or the pause_time field).
Total Duration Today: Complete time logged in today. If active, it catches their real-time clock from their last known login (exactly like User Summary tabs).
Idle Time: Time logged in minus (Work + Pause).
Status: "Working", "Paused" (if files[].file_status == "pause"), or "Login/Logout".
(Expandable Detail): Lists every individual pause instance from the pause_reasons[] array, showing the exact reason, started_at, completed_at, and duration for each break.

Comment
Ctrl+Alt+M
