# User Guide — ClearFolder Audit 0.2.0

## Quick start

1. Run `FolderAuditTool.exe` from the `dist` folder.
2. Click **Browse...** and choose a folder.
3. Optionally set filters:
   - **Exclude extensions**: `.tmp,.log` or `tmp,log`.
   - **Exclude folders**: `node_modules,.git`.
   - **Min file size**: ignore files smaller than the selected size.
4. Click **Scan**.
5. Review the tabs.
6. Export results with **Export HTML report** or **Export CSV files**.

## Tabs

### Top folders

Shows the largest subfolders and their percent of total scanned size. This helps find the main storage sources quickly.

### Largest files

Shows the largest individual files.

### Duplicate groups

Shows exact duplicate files by SHA-256 hash. Each row is a file in a duplicate group. The app reports potential wasted space but does not choose what to delete.

### Empty folders

Shows folders that appear empty in the filtered scan.

### Extensions

Shows file count and total size per extension.

### Age

Shows size and count by modified-date buckets:

- 0–7 days
- 8–30 days
- 31–90 days
- 91–365 days
- Over 1 year

### Safe cleanup plan

A prioritized, read-only checklist:

1. Duplicate review items.
2. Old large files over 10 MB and older than one year.
3. Empty folders.

This is the product's main differentiator: it translates scan data into a safe manual review plan without deleting anything.

### Log

Shows skipped reparse points, excluded extensions/folders, below-minimum-size files, and access errors.

## Sorting and Explorer actions

- Click a column header to sort results.
- Double-click a row to select/open it in Windows Explorer.
- Right-click a row to:
  - open in Explorer;
  - select in Explorer;
  - copy path.

## Export files

CSV export creates:

- `files.csv`
- `largest_files.csv`
- `top_folders.csv`
- `duplicates.csv`
- `empty_folders.csv`
- `extensions.csv`
- `age.csv`
- `cleanup_plan.csv`
- `skipped_items.txt`

HTML export includes visual bar charts for top folders, extension distribution, and file age.

## Cancellation

Click **Cancel** during a scan. The current scan stops and no files are changed.

## Privacy

The app works locally. It does not upload files or scan results anywhere.

## Limitations

- No treemap yet.
- No direct NTFS MFT fast scan.
- Duplicate detection is exact hash matching, not fuzzy similarity.
- EXE is unsigned.
