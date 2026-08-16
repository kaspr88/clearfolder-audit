# BUILD_REPORT — ClearFolder Audit v0.2

Date: 2026-08-16

## Summary

Built **ClearFolder Audit 0.2.0**, a portable read-only Windows GUI product for folder audits.

Portable executable:

```text
dist\FolderAuditTool.exe
```

Observed EXE size: 63,488 bytes.

## v0.2 goals and status

| Requirement | Status |
|---|---|
| TOP subfolders by size | Done |
| Percent of total per subfolder | Done |
| HTML visualization: top folders | Done |
| HTML visualization: extensions | Done |
| HTML visualization: age | Done |
| Excluded extensions filter | Done |
| Excluded folders filter | Done |
| Minimum file size filter | Done |
| Improved GUI tabs/sections | Done |
| Display results in app | Done |
| Open/select file/folder in Explorer | Done |
| Sort result columns | Done |
| Clearer duplicate group display | Done |
| Version info | Done |
| Product name | Done: ClearFolder Audit |
| Icon if possible without installing software | Done: runtime-generated WinForms icon |
| Load test on at least 10,000 files | Done |
| Measure execution time | Done |
| Test cancellation | Done |
| Test access errors | Done with locked-file hash access scenario |
| Rebuild portable EXE | Done |
| Update README / USER_GUIDE / CHANGELOG / BUILD_REPORT | Done |

## Distinguishing feature added

Added **Safe Cleanup Plan**.

This is a read-only, prioritized checklist that converts raw scan results into manual review actions:

1. Duplicate review items.
2. Old large files over 10 MB and older than one year.
3. Empty folders.

The app still does not delete anything. The value proposition is not “one-click cleanup”; it is “safe evidence before cleanup.”

## Competitor research and comparison

Sources reviewed:

- WinDirStat official/GitHub: disk usage analyzer with directory tree, treemap, extension stats, duplicate detection, filters, cleanup/file actions. https://windirstat.dev/ and https://github.com/windirstat/windirstat
- TreeSize Free/manual: disk space manager with exports/charts and scan filters. https://www.jam-software.com/treesize/features.shtml and https://manuals.jam-software.de/treesize/EN/scheduler_export_tab.html
- WizTree official: very fast disk analyzer, especially via NTFS MFT scanning; treemap; free for personal use. https://wize-tree.com/
- dupeGuru: open-source duplicate finder focused on duplicate matching. https://dupeguru.net/

### Why should a user install ClearFolder Audit?

Honest answer: if the user wants the fastest or most feature-rich disk analyzer, WizTree, TreeSize Free, or WinDirStat are stronger mature products.

ClearFolder Audit is compelling only for a narrower niche:

- users who want a **read-only report** before cleanup;
- freelancers who need to deliver an **HTML/CSV audit package** to a client;
- office/shared-drive users who need a **manual cleanup checklist** instead of a deletion tool;
- non-technical users who want a simpler Browse → Scan → Review → Export workflow.

The strongest differentiator is **Safe Cleanup Plan + exportable evidence**. If this niche is not pursued, the product should pivot away from generic disk analysis because mature free competitors are too strong.

## Implemented v0.2 files

Source:

- `src\FolderAudit.Core.cs`
- `src\MainForm.cs`
- `src\Program.cs`

Build/test:

- `Build.ps1`
- `Test.ps1`

Docs:

- `README.md`
- `USER_GUIDE.md`
- `CHANGELOG.md`
- `LICENSE`
- `PRODUCT_DESCRIPTION.md`
- `BUILD_REPORT.md`

Output:

- `dist\FolderAuditTool.exe`

Test artifacts:

- `test-data\exports\report.html`
- `test-data\exports\files.csv`
- `test-data\exports\largest_files.csv`
- `test-data\exports\top_folders.csv`
- `test-data\exports\duplicates.csv`
- `test-data\exports\empty_folders.csv`
- `test-data\exports\extensions.csv`
- `test-data\exports\age.csv`
- `test-data\exports\cleanup_plan.csv`
- `test-data\exports\skipped_items.txt`
- `test-data\test-summary.txt`

## Build result

Command:

```powershell
powershell -ExecutionPolicy Bypass -File .\Build.ps1
```

Observed:

```text
Built: <project>\dist\FolderAuditTool.exe
Length: 63488 bytes
```

## Functional test result

Command:

```powershell
powershell -ExecutionPolicy Bypass -File .\Test.ps1
```

Observed:

```text
All v0.2 functional tests passed.
Load test: 10000 files, 101 folders, 950 ms.
```

Validated:

- recursive scan;
- total size;
- file/folder counts;
- duplicate detection by SHA-256;
- empty folder detection;
- top subfolder stats;
- extension distribution;
- age distribution;
- excluded extension filter;
- excluded folder filter;
- minimum-size option path;
- HTML report creation;
- HTML contains top folder, extension, age visualizations and Safe Cleanup Plan;
- CSV files including `top_folders.csv` and `cleanup_plan.csv`;
- pre-cancelled scan cancellation path;
- locked-file hash access error logging;
- 10,000 file load test.

## GUI smoke test

The EXE was launched and verified to create a main window.

Observed:

```text
Started: True
Title: ClearFolder Audit 0.2.0
```

The app closed cleanly.

## Screenshot status

Screenshots were previously attempted with the bundled Windows screenshot helper, but capture failed with:

```text
CopyFromScreen: The handle is invalid
```

The GUI itself launches successfully. No screenshot artifact was produced in this runtime.

## Known limitations

- No treemap visualization.
- No direct NTFS MFT fast scan, so WizTree remains much faster for whole-drive analysis.
- No folder history/comparison mode yet.
- No scheduled command-line report mode yet.
- Duplicate detection is exact SHA-256 only.
- Old-large-file threshold is currently fixed at 10 MB and 1 year.
- EXE is unsigned; SmartScreen may warn users.
- Runtime-generated icon appears in the app window but is not embedded as a proper executable resource icon.

## Recommended v0.3

Focus on the chosen niche instead of chasing generic disk analyzer features:

1. Add **before/after audit comparison**: compare two scan exports and show storage changes.
2. Add **branded client report mode** for freelancers/offices.
3. Add configurable cleanup-plan thresholds.
4. Add folder tree summary with top nested folders.
5. Add command-line mode for scheduled reports.
6. Add simple HTML charts with cleaner design and printable summary page.
