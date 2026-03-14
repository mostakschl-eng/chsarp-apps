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

Redesigning File Explorer UI & Workflow
This plan transforms the "Files Box" into a modern, Windows-style navigation system, fixing the "heavy" network performance issues and simplifying the UI.

Performance Optimization (Surface Scan)
[Component] "Surface Scan" Logic
The Problem: The app currently scans every subfolder recursively (Deep Scan), which is extremely slow on network drives (P:).
The Solution: We will switch to a "Surface Scan" (Shallow). The app will load ONLY what you see in the current folder, matching the speed of Windows File Explorer.
Proposed Changes
[Component] UI Cleanup (Simplification)
[DELETE] Redundant Buttons
Remove the following buttons from the bottom of the Files Box in 
ExplorerV2View.xaml
:
Work
, Production Done, QC1 Done, QC2 Done, Ready To Upload.
Reason: Users no longer need toggle filters because they can simply navigate directly to these folders like they do in Windows.
[DELETE] Automatic Filtering Logic
Remove code in 
ExplorerV2View.xaml.cs
 and 
FileIndexService.cs
 that automatically hides "Done" or "Backup" folders.
Reason: The user should have full control to see and open any folder they need.
[Component] Windows-Style Navigation
[NEW] Navigation Controls
Back Button: Added to the top of the Files Box.
Path Bar (Breadcrumbs): Shows the current folder path (e.g., Job77 \ Production \ Final).
Icons:
Folders: Show a standard yellow folder icon.
Files: Show current JPG/PSD icons.
[MODIFY] 
ExplorerV2View.xaml.cs
Double-Click: Open subfolders instantly.
Surface Scan: Update LoadFilesAsync to use SearchOption.TopDirectoryOnly for maximum speed.
[MODIFY] 
FileTileItem.cs
Add IsFolder property.
Verification Plan
Manual Verification
Speed Test: Open a large job; it should load instantly.
Double-Click: Navigate deep into subfolders (e.g., Production/Done) and back out using the Back button.
UI Cleanliness: Verify the bottom button bar is gone, leaving more room for files.
Icons: Confirm folders and files are visually distinct.





You are completely right, and I apologize for missing that. I just scanned your project structure to see exactly how you have it set up.

Since you already have a well-defined architecture with a hooks folder in the portal and a shared common package for types, we MUST follow that exact structure. We should not create random folders inside the page route.

Here is the exact, corrected file structure following your existing Pro setup:

1. The Route & UI Components (in the Portal)
apps/schl-portal/src/app/(pages)/live-tracking/page.tsx (The main page)
apps/schl-portal/src/app/(pages)/live-tracking/components/LiveTrackingDashboard.tsx (Main layout component)
apps/schl-portal/src/app/(pages)/live-tracking/components/UserStatsCard.tsx
apps/schl-portal/src/app/(pages)/live-tracking/components/ActiveJobsTable.tsx (Note: It is still best practice in Next.js to put page-specific UI components here so they don't clutter your global shared components folder, but let me know if you prefer them in src/components/live-tracking instead!)
2. The Hook (in the Portal's Global Hooks)
apps/schl-portal/src/hooks/useLiveTracking.ts (This sits right next to your existing usePaginationManager.ts)
3. The Types/Interfaces (in the Shared Package)
packages/common/src/types/live-tracking.type.ts (This sits right next to your existing permission.type.ts and user-session.type.ts, making the types available to both the frontend and your backend/API)
4. The Navigation Link
apps/schl-portal/src/components/Header.tsx (Add the /live-tracking link)
This is the exact right way to do it in your Turborepo. Do you want me to start creating these files now?