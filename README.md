# ClearFolder Audit

**ClearFolder Audit** is a small, portable, read-only Windows folder audit tool for people who want evidence before deleting anything.

It scans a selected folder and creates a safe cleanup report with top subfolders by size, largest files, SHA-256 duplicate groups, empty folders, extension distribution, file age, a manual Safe Cleanup Plan, and HTML/CSV exports.

The app is intentionally **read-only**. It does **not** delete, move, rename, upload, or modify your files.

![ClearFolder Audit sample report](demo/clearfolder-audit-preview.svg)

## Try it / download

- **Supporter download on Gumroad:** https://kasprian.gumroad.com/l/clearfolder-audit
- **GitHub release:** https://github.com/kaspr88/clearfolder-audit/releases/tag/v0.2.0
- **Sample HTML report:** [demo/sample-report.html](demo/sample-report.html)

The Gumroad ZIP includes the portable Windows EXE, documentation, license, and sample report. This repository contains the source code and demo materials so users can inspect what the tool does before downloading or purchasing the packaged build.

## Why this exists

WizTree, TreeSize, and WinDirStat are excellent disk analyzers. ClearFolder Audit has a narrower goal: a simple, safe, evidence-first folder audit report before cleanup.

This is useful when you need to review or explain a cleanup plan for:

- shared folders;
- client/project archives;
- office folders;
- old backups;
- messy Downloads/Desktop folders;
- any folder where deletion should be reviewed first.

## What the report includes

- Top subfolders by size and percent of total.
- Largest files.
- Exact duplicate groups using SHA-256 hashes.
- Empty folders.
- File types by extension.
- File age distribution.
- Manual Safe Cleanup Plan.
- HTML report with visual bar charts.
- CSV export bundle for spreadsheet review.
- Skipped/access-denied items log.

## Safety model

ClearFolder Audit is designed as an audit/reporting tool, not an automatic cleaner.

- No delete button.
- No move or rename operation.
- No uploads or telemetry.
- Files are opened read-only for hashing.
- Reparse points are skipped by default to avoid loops and unexpected traversal.
- Safe Cleanup Plan items are suggestions for manual review only.

## Build

```powershell
powershell -ExecutionPolicy Bypass -File .\Build.ps1
```

Output:

```text
dist\FolderAuditTool.exe
```

## Test

```powershell
powershell -ExecutionPolicy Bypass -File .\Test.ps1
```

The test script creates synthetic data, validates core results, checks exports, tests cancellation/access handling, and runs a 10,000-file load test.

## Current limitations

- Windows-only.
- Unsigned EXE in current release, so Windows SmartScreen may warn.
- No treemap.
- No direct NTFS MFT fast scan.
- Exact duplicate detection only.
- Safe Cleanup Plan is advisory and manual; it does not delete files.

## Feedback wanted

I am testing whether the **read-only folder audit report + cleanup plan** angle is useful enough, especially for shared folders, office folders, client folders, and cleanup work where evidence matters.

If you try it, feedback on positioning, missing report sections, and trust blockers is especially useful.

## License

MIT License. See [LICENSE](LICENSE).
