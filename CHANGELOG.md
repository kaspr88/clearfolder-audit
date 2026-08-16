# Changelog

## 0.2.0 — 2026-08-16

### Added

- Product name changed to **ClearFolder Audit**.
- Version shown in window title and About dialog.
- Runtime-generated app icon.
- TOP subfolders by size with percent of total.
- HTML bar-chart visualizations:
  - top subfolders;
  - extension distribution;
  - file age distribution.
- Scan filters:
  - excluded extensions;
  - excluded folder names;
  - minimum file size.
- GUI tab for top folders.
- GUI tab for Safe Cleanup Plan.
- Sortable result columns.
- Right-click context menu for results:
  - open in Explorer;
  - select in Explorer;
  - copy path.
- Clearer duplicate group display.
- `top_folders.csv` export.
- `cleanup_plan.csv` export.
- Locked-file/access error test.
- Load test with 10,000 files and timing.

### Changed

- HTML report now includes filters, version, visual charts, top folders, and cleanup plan.
- CSV export bundle expanded.
- Documentation updated for v0.2.

### Safety

- Still read-only: no delete, move, rename, upload, or file modification features.

## 0.1.0 MVP — 2026-08-16

### Added

- Windows WinForms graphical interface.
- Folder selection dialog.
- Recursive read-only folder scan.
- Total size calculation.
- File and folder counts.
- Top largest files tab.
- Exact duplicate detection by SHA-256 hash.
- Empty folder detection.
- Extension distribution tab.
- File age bucket tab.
- Progress indicator with live status counts.
- Cancel scan button.
- HTML report export.
- CSV export bundle.
- Log of skipped files/folders and access issues.
- Build script using PowerShell/.NET Framework compiler.
- Functional test script and synthetic test data generation.
- README, user guide, license, product description, and build report.
