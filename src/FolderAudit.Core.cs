using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace FolderAuditTool
{
    public sealed class AuditOptions
    {
        public int TopLargestCount { get; set; }
        public bool SkipReparsePoints { get; set; }
        public HashSet<string> ExcludedExtensions { get; set; }
        public HashSet<string> ExcludedFolderNames { get; set; }
        public long MinFileSizeBytes { get; set; }

        public AuditOptions()
        {
            TopLargestCount = 50;
            SkipReparsePoints = true;
            ExcludedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ExcludedFolderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            MinFileSizeBytes = 0;
        }

        public static HashSet<string> ParseExtensions(string text)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in SplitList(text))
            {
                string value = raw.Trim();
                if (value.Length == 0) continue;
                if (!value.StartsWith(".")) value = "." + value;
                set.Add(value.ToLowerInvariant());
            }
            return set;
        }

        public static HashSet<string> ParseFolderNames(string text)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in SplitList(text))
            {
                string value = raw.Trim().Trim('\\', '/');
                if (value.Length > 0) set.Add(value);
            }
            return set;
        }

        private static IEnumerable<string> SplitList(string text)
        {
            if (String.IsNullOrWhiteSpace(text)) yield break;
            foreach (string part in text.Split(new[] { ',', ';', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)) yield return part;
        }
    }

    public sealed class ScanProgress
    {
        public string CurrentPath { get; set; }
        public int FilesScanned { get; set; }
        public int FoldersScanned { get; set; }
        public long BytesScanned { get; set; }
        public string Phase { get; set; }
    }

    public sealed class FileRecord
    {
        public string Path { get; set; }
        public long Size { get; set; }
        public string Extension { get; set; }
        public DateTime LastWriteTime { get; set; }
        public string Sha256 { get; set; }
    }

    public sealed class FolderRecord
    {
        public string Path { get; set; }
    }

    public sealed class FolderSizeStat
    {
        public string Path { get; set; }
        public long Bytes { get; set; }
        public int FileCount { get; set; }
        public int FolderCount { get; set; }
        public double PercentOfTotal { get; set; }
    }

    public sealed class DuplicateGroup
    {
        public string Sha256 { get; set; }
        public long Size { get; set; }
        public List<FileRecord> Files { get; set; }
        public long WastedBytes { get { return Math.Max(0, Files.Count - 1) * Size; } }

        public DuplicateGroup()
        {
            Files = new List<FileRecord>();
        }
    }

    public sealed class ExtensionStat
    {
        public string Extension { get; set; }
        public int Count { get; set; }
        public long Bytes { get; set; }
        public double PercentOfTotal { get; set; }
    }

    public sealed class AgeStat
    {
        public string Bucket { get; set; }
        public int Count { get; set; }
        public long Bytes { get; set; }
        public double PercentOfTotal { get; set; }
    }

    public sealed class CleanupSuggestion
    {
        public string Category { get; set; }
        public string Path { get; set; }
        public long Bytes { get; set; }
        public string Reason { get; set; }
        public int Priority { get; set; }
    }

    public sealed class AuditResult
    {
        public string RootPath { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime FinishedAt { get; set; }
        public bool Cancelled { get; set; }
        public int FileCount { get; set; }
        public int FolderCount { get; set; }
        public long TotalBytes { get; set; }
        public AuditOptions Options { get; set; }
        public List<FileRecord> Files { get; set; }
        public List<FileRecord> LargestFiles { get; set; }
        public List<DuplicateGroup> DuplicateGroups { get; set; }
        public List<FolderRecord> EmptyFolders { get; set; }
        public List<FolderSizeStat> TopFolders { get; set; }
        public List<ExtensionStat> ExtensionStats { get; set; }
        public List<AgeStat> AgeStats { get; set; }
        public List<CleanupSuggestion> CleanupPlan { get; set; }
        public List<string> SkippedItems { get; set; }

        internal Dictionary<string, long> FolderBytes { get; set; }
        internal Dictionary<string, int> FolderFileCounts { get; set; }
        internal Dictionary<string, int> FolderChildFolderCounts { get; set; }

        public AuditResult()
        {
            Files = new List<FileRecord>();
            LargestFiles = new List<FileRecord>();
            DuplicateGroups = new List<DuplicateGroup>();
            EmptyFolders = new List<FolderRecord>();
            TopFolders = new List<FolderSizeStat>();
            ExtensionStats = new List<ExtensionStat>();
            AgeStats = new List<AgeStat>();
            CleanupPlan = new List<CleanupSuggestion>();
            SkippedItems = new List<string>();
            FolderBytes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            FolderFileCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            FolderChildFolderCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        public long DuplicateWastedBytes
        {
            get { return DuplicateGroups.Sum(g => g.WastedBytes); }
        }
    }

    public static class FolderScanner
    {
        public static AuditResult Scan(string rootPath, AuditOptions options, IProgress<ScanProgress> progress, CancellationToken token)
        {
            if (String.IsNullOrWhiteSpace(rootPath)) throw new ArgumentException("Folder path is empty.");
            if (!Directory.Exists(rootPath)) throw new DirectoryNotFoundException(rootPath);
            if (options == null) options = new AuditOptions();

            AuditResult result = new AuditResult();
            result.RootPath = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            result.Options = options;
            result.StartedAt = DateTime.Now;

            WalkDirectory(new DirectoryInfo(result.RootPath), result, options, progress, token, true);
            token.ThrowIfCancellationRequested();

            Report(progress, result, result.RootPath, "Hashing duplicate candidates");
            BuildDerivedResults(result, options, progress, token);
            result.FinishedAt = DateTime.Now;
            return result;
        }

        private static bool WalkDirectory(DirectoryInfo dir, AuditResult result, AuditOptions options, IProgress<ScanProgress> progress, CancellationToken token, bool isRoot)
        {
            token.ThrowIfCancellationRequested();

            if (!isRoot && options.ExcludedFolderNames.Contains(dir.Name))
            {
                result.SkippedItems.Add("Excluded folder: " + dir.FullName);
                return false;
            }

            if (options.SkipReparsePoints && IsReparsePoint(dir.Attributes))
            {
                result.SkippedItems.Add("Skipped reparse folder: " + dir.FullName);
                return false;
            }

            result.FolderCount++;
            EnsureFolder(result, dir.FullName);
            Report(progress, result, dir.FullName, "Scanning folders and files");

            FileInfo[] files = new FileInfo[0];
            DirectoryInfo[] subdirs = new DirectoryInfo[0];
            bool hasVisibleFile = false;
            bool hasVisibleDir = false;

            try { files = dir.GetFiles(); }
            catch (Exception ex) { result.SkippedItems.Add("Cannot list files: " + dir.FullName + " (" + ex.Message + ")"); }

            foreach (FileInfo file in files)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    if (options.SkipReparsePoints && IsReparsePoint(file.Attributes))
                    {
                        result.SkippedItems.Add("Skipped reparse file: " + file.FullName);
                        continue;
                    }

                    string ext = NormalizeExtension(file.Extension);
                    if (options.ExcludedExtensions.Contains(ext))
                    {
                        result.SkippedItems.Add("Excluded extension: " + file.FullName);
                        continue;
                    }
                    if (file.Length < options.MinFileSizeBytes)
                    {
                        result.SkippedItems.Add("Below minimum size: " + file.FullName);
                        continue;
                    }

                    FileRecord rec = new FileRecord();
                    rec.Path = file.FullName;
                    rec.Size = file.Length;
                    rec.Extension = ext;
                    rec.LastWriteTime = file.LastWriteTime;
                    result.Files.Add(rec);
                    result.FileCount++;
                    result.TotalBytes += rec.Size;
                    hasVisibleFile = true;
                    AddFileToFolderRollup(result, file.DirectoryName, rec.Size);

                    if (result.FileCount % 100 == 0) Report(progress, result, file.FullName, "Scanning folders and files");
                }
                catch (Exception ex)
                {
                    result.SkippedItems.Add("Cannot read file info: " + file.FullName + " (" + ex.Message + ")");
                }
            }

            try { subdirs = dir.GetDirectories(); }
            catch (Exception ex) { result.SkippedItems.Add("Cannot list folders: " + dir.FullName + " (" + ex.Message + ")"); }

            foreach (DirectoryInfo subdir in subdirs)
            {
                token.ThrowIfCancellationRequested();
                bool childHasContent = WalkDirectory(subdir, result, options, progress, token, false);
                if (childHasContent) hasVisibleDir = true;
                if (!options.ExcludedFolderNames.Contains(subdir.Name)) IncrementFolderChildCount(result, dir.FullName);
            }

            bool hasAnyContent = hasVisibleFile || hasVisibleDir;
            if (!hasAnyContent && !isRoot) result.EmptyFolders.Add(new FolderRecord { Path = dir.FullName });
            return hasAnyContent;
        }

        private static void EnsureFolder(AuditResult result, string folder)
        {
            if (String.IsNullOrEmpty(folder)) return;
            if (!result.FolderBytes.ContainsKey(folder)) result.FolderBytes[folder] = 0;
            if (!result.FolderFileCounts.ContainsKey(folder)) result.FolderFileCounts[folder] = 0;
            if (!result.FolderChildFolderCounts.ContainsKey(folder)) result.FolderChildFolderCounts[folder] = 0;
        }

        private static void IncrementFolderChildCount(AuditResult result, string folder)
        {
            EnsureFolder(result, folder);
            result.FolderChildFolderCounts[folder] = result.FolderChildFolderCounts[folder] + 1;
        }

        private static void AddFileToFolderRollup(AuditResult result, string folder, long bytes)
        {
            if (String.IsNullOrEmpty(folder)) return;
            DirectoryInfo current = new DirectoryInfo(folder);
            string root = result.RootPath;
            while (current != null)
            {
                string full = current.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                EnsureFolder(result, full);
                result.FolderBytes[full] = result.FolderBytes[full] + bytes;
                result.FolderFileCounts[full] = result.FolderFileCounts[full] + 1;
                if (String.Equals(full, root, StringComparison.OrdinalIgnoreCase)) break;
                current = current.Parent;
            }
        }

        private static bool IsReparsePoint(FileAttributes attrs)
        {
            return (attrs & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
        }

        private static void BuildDerivedResults(AuditResult result, AuditOptions options, IProgress<ScanProgress> progress, CancellationToken token)
        {
            result.LargestFiles = result.Files.OrderByDescending(f => f.Size).Take(options.TopLargestCount).ToList();

            result.TopFolders = result.FolderBytes
                .Where(kv => !String.Equals(kv.Key.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), result.RootPath, StringComparison.OrdinalIgnoreCase) && kv.Value > 0)
                .Select(kv => new FolderSizeStat
                {
                    Path = kv.Key,
                    Bytes = kv.Value,
                    FileCount = result.FolderFileCounts.ContainsKey(kv.Key) ? result.FolderFileCounts[kv.Key] : 0,
                    FolderCount = result.FolderChildFolderCounts.ContainsKey(kv.Key) ? result.FolderChildFolderCounts[kv.Key] : 0,
                    PercentOfTotal = result.TotalBytes == 0 ? 0 : (100.0 * kv.Value / result.TotalBytes)
                })
                .OrderByDescending(s => s.Bytes)
                .Take(50)
                .ToList();

            result.ExtensionStats = result.Files
                .GroupBy(f => f.Extension)
                .Select(g => new ExtensionStat { Extension = g.Key, Count = g.Count(), Bytes = g.Sum(f => f.Size), PercentOfTotal = result.TotalBytes == 0 ? 0 : 100.0 * g.Sum(f => f.Size) / result.TotalBytes })
                .OrderByDescending(s => s.Bytes)
                .ThenBy(s => s.Extension)
                .ToList();

            DateTime now = DateTime.Now;
            var ageBuckets = new Dictionary<string, AgeStat>();
            string[] labels = new[] { "0-7 days", "8-30 days", "31-90 days", "91-365 days", "Over 1 year" };
            foreach (string label in labels) ageBuckets[label] = new AgeStat { Bucket = label };

            foreach (FileRecord f in result.Files)
            {
                double days = (now - f.LastWriteTime).TotalDays;
                string bucket = days <= 7 ? "0-7 days" : days <= 30 ? "8-30 days" : days <= 90 ? "31-90 days" : days <= 365 ? "91-365 days" : "Over 1 year";
                ageBuckets[bucket].Count++;
                ageBuckets[bucket].Bytes += f.Size;
            }
            foreach (AgeStat s in ageBuckets.Values) s.PercentOfTotal = result.TotalBytes == 0 ? 0 : 100.0 * s.Bytes / result.TotalBytes;
            result.AgeStats = labels.Select(l => ageBuckets[l]).ToList();

            var sizeGroups = result.Files.GroupBy(f => f.Size).Where(g => g.Key > 0 && g.Count() > 1).ToList();
            int processed = 0;
            int total = sizeGroups.Sum(g => g.Count());
            foreach (var group in sizeGroups)
            {
                token.ThrowIfCancellationRequested();
                foreach (FileRecord file in group)
                {
                    token.ThrowIfCancellationRequested();
                    try { file.Sha256 = ComputeSha256(file.Path); }
                    catch (Exception ex) { result.SkippedItems.Add("Cannot hash file: " + file.Path + " (" + ex.Message + ")"); }
                    processed++;
                    if (processed % 25 == 0) Report(progress, result, file.Path, "Hashing duplicate candidates " + processed + "/" + total);
                }
            }

            result.DuplicateGroups = result.Files
                .Where(f => !String.IsNullOrEmpty(f.Sha256))
                .GroupBy(f => f.Sha256)
                .Where(g => g.Count() > 1)
                .Select(g => new DuplicateGroup { Sha256 = g.Key, Size = g.First().Size, Files = g.OrderBy(f => f.Path).ToList() })
                .OrderByDescending(g => g.WastedBytes)
                .ThenByDescending(g => g.Size)
                .ToList();

            BuildCleanupPlan(result);
        }

        private static void BuildCleanupPlan(AuditResult result)
        {
            var suggestions = new List<CleanupSuggestion>();
            foreach (var g in result.DuplicateGroups.Take(100))
            {
                foreach (var f in g.Files)
                {
                    suggestions.Add(new CleanupSuggestion
                    {
                        Category = "Duplicate review",
                        Path = f.Path,
                        Bytes = g.WastedBytes,
                        Priority = 1,
                        Reason = "Exact SHA-256 duplicate group. Review manually; keep at least one copy. Potential group waste: " + ReportExporter.FormatBytes(g.WastedBytes) + "."
                    });
                }
            }

            DateTime cutoff = DateTime.Now.AddDays(-365);
            foreach (var f in result.Files.Where(f => f.Size >= 10L * 1024L * 1024L && f.LastWriteTime < cutoff).OrderByDescending(f => f.Size).Take(50))
            {
                suggestions.Add(new CleanupSuggestion
                {
                    Category = "Old large file",
                    Path = f.Path,
                    Bytes = f.Size,
                    Priority = 2,
                    Reason = "File is over 10 MB and has not been modified for more than one year. Review before archiving or deleting."
                });
            }

            foreach (var f in result.EmptyFolders.Take(100))
            {
                suggestions.Add(new CleanupSuggestion
                {
                    Category = "Empty folder",
                    Path = f.Path,
                    Bytes = 0,
                    Priority = 3,
                    Reason = "Folder appears empty in this filtered scan. Review manually before removing."
                });
            }

            result.CleanupPlan = suggestions.OrderBy(s => s.Priority).ThenByDescending(s => s.Bytes).Take(250).ToList();
        }

        private static string ComputeSha256(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(stream);
                StringBuilder sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        private static string NormalizeExtension(string ext)
        {
            if (String.IsNullOrWhiteSpace(ext)) return "[no extension]";
            return ext.ToLowerInvariant();
        }

        private static void Report(IProgress<ScanProgress> progress, AuditResult result, string path, string phase)
        {
            if (progress == null) return;
            progress.Report(new ScanProgress
            {
                CurrentPath = path,
                FilesScanned = result.FileCount,
                FoldersScanned = result.FolderCount,
                BytesScanned = result.TotalBytes,
                Phase = phase
            });
        }
    }

    public static class ReportExporter
    {
        public static void ExportCsv(AuditResult result, string folderPath)
        {
            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
            WriteCsv(Path.Combine(folderPath, "files.csv"), new[] { "Path", "SizeBytes", "Extension", "LastWriteTime", "Sha256" }, result.Files.Select(f => new[] { f.Path, f.Size.ToString(), f.Extension, f.LastWriteTime.ToString("s"), f.Sha256 ?? "" }));
            WriteCsv(Path.Combine(folderPath, "largest_files.csv"), new[] { "Path", "SizeBytes", "SizeHuman", "Extension", "LastWriteTime" }, result.LargestFiles.Select(f => new[] { f.Path, f.Size.ToString(), FormatBytes(f.Size), f.Extension, f.LastWriteTime.ToString("s") }));
            WriteCsv(Path.Combine(folderPath, "top_folders.csv"), new[] { "Path", "Bytes", "SizeHuman", "PercentOfTotal", "FileCount", "ChildFolderCount" }, result.TopFolders.Select(s => new[] { s.Path, s.Bytes.ToString(), FormatBytes(s.Bytes), s.PercentOfTotal.ToString("0.00"), s.FileCount.ToString(), s.FolderCount.ToString() }));
            WriteCsv(Path.Combine(folderPath, "duplicates.csv"), new[] { "GroupHash", "GroupSizeBytes", "WastedBytes", "FilePath" }, result.DuplicateGroups.SelectMany(g => g.Files.Select(f => new[] { g.Sha256, g.Size.ToString(), g.WastedBytes.ToString(), f.Path })));
            WriteCsv(Path.Combine(folderPath, "empty_folders.csv"), new[] { "Path" }, result.EmptyFolders.Select(f => new[] { f.Path }));
            WriteCsv(Path.Combine(folderPath, "extensions.csv"), new[] { "Extension", "Count", "Bytes", "SizeHuman", "PercentOfTotal" }, result.ExtensionStats.Select(s => new[] { s.Extension, s.Count.ToString(), s.Bytes.ToString(), FormatBytes(s.Bytes), s.PercentOfTotal.ToString("0.00") }));
            WriteCsv(Path.Combine(folderPath, "age.csv"), new[] { "Bucket", "Count", "Bytes", "SizeHuman", "PercentOfTotal" }, result.AgeStats.Select(s => new[] { s.Bucket, s.Count.ToString(), s.Bytes.ToString(), FormatBytes(s.Bytes), s.PercentOfTotal.ToString("0.00") }));
            WriteCsv(Path.Combine(folderPath, "cleanup_plan.csv"), new[] { "Priority", "Category", "Path", "Bytes", "SizeHuman", "Reason" }, result.CleanupPlan.Select(s => new[] { s.Priority.ToString(), s.Category, s.Path, s.Bytes.ToString(), FormatBytes(s.Bytes), s.Reason }));
            File.WriteAllLines(Path.Combine(folderPath, "skipped_items.txt"), result.SkippedItems.ToArray(), Encoding.UTF8);
        }

        private static void WriteCsv(string path, string[] headers, IEnumerable<string[]> rows)
        {
            using (var writer = new StreamWriter(path, false, new UTF8Encoding(true)))
            {
                writer.WriteLine(String.Join(",", headers.Select(EscapeCsv)));
                foreach (var row in rows) writer.WriteLine(String.Join(",", row.Select(EscapeCsv)));
            }
        }

        private static string EscapeCsv(string value)
        {
            if (value == null) value = "";
            if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0) return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }

        public static void ExportHtml(AuditResult result, string path)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<!doctype html><html><head><meta charset='utf-8'><title>Folder Audit Report</title>");
            sb.AppendLine("<style>body{font-family:Segoe UI,Arial,sans-serif;margin:24px;color:#222}table{border-collapse:collapse;width:100%;margin:12px 0 28px}th,td{border:1px solid #ddd;padding:6px 8px;text-align:left}th{background:#f2f4f8}.num{text-align:right}.card{display:inline-block;border:1px solid #ddd;border-radius:8px;padding:12px 16px;margin:6px 10px 6px 0;background:#fafafa}.bar{height:20px;background:#4f7cff;border-radius:3px;color:white;padding-left:6px;white-space:nowrap}.bar2{background:#14a38b}.bar3{background:#a05ad9}.warn{color:#9a5b00}.small{font-size:12px;color:#666}</style></head><body>");
            sb.AppendLine("<h1>Folder Audit Report</h1>");
            sb.AppendLine("<p><b>Root:</b> " + Html(result.RootPath) + "<br><b>Started:</b> " + result.StartedAt + "<br><b>Finished:</b> " + result.FinishedAt + "<br><b>Tool version:</b> " + Html(AppInfo.Version) + "</p>");
            sb.AppendLine("<p class='small'><b>Filters:</b> excluded extensions " + Html(JoinOrNone(result.Options.ExcludedExtensions)) + "; excluded folders " + Html(JoinOrNone(result.Options.ExcludedFolderNames)) + "; minimum file size " + Html(FormatBytes(result.Options.MinFileSizeBytes)) + ".</p>");
            sb.AppendLine(Card("Total size", FormatBytes(result.TotalBytes)) + Card("Files", result.FileCount.ToString()) + Card("Folders", result.FolderCount.ToString()) + Card("Duplicate groups", result.DuplicateGroups.Count.ToString()) + Card("Potential duplicate waste", FormatBytes(result.DuplicateWastedBytes)) + Card("Empty folders", result.EmptyFolders.Count.ToString()));
            AppendBarChart(sb, "Top subfolders by size", result.TopFolders.Take(15).Select(s => new ChartItem { Label = ShortPath(result.RootPath, s.Path), Bytes = s.Bytes, Percent = s.PercentOfTotal }), "bar");
            AppendBarChart(sb, "Distribution by extension", result.ExtensionStats.Take(15).Select(s => new ChartItem { Label = s.Extension, Bytes = s.Bytes, Percent = s.PercentOfTotal }), "bar2");
            AppendBarChart(sb, "Distribution by file age", result.AgeStats.Select(s => new ChartItem { Label = s.Bucket, Bytes = s.Bytes, Percent = s.PercentOfTotal }), "bar3");
            AppendSimpleTable(sb, "Top subfolders", new[] { "Folder", "Size", "% of total", "Files" }, result.TopFolders.Select(s => new[] { s.Path, FormatBytes(s.Bytes), s.PercentOfTotal.ToString("0.00") + "%", s.FileCount.ToString() }));
            AppendSimpleTable(sb, "Safe cleanup plan", new[] { "Priority", "Category", "Path", "Size", "Reason" }, result.CleanupPlan.Select(s => new[] { s.Priority.ToString(), s.Category, s.Path, FormatBytes(s.Bytes), s.Reason }));
            AppendFileTable(sb, "Top largest files", result.LargestFiles);
            sb.AppendLine("<h2>Duplicate files by SHA-256</h2>");
            if (result.DuplicateGroups.Count == 0) sb.AppendLine("<p>No duplicate files found.</p>");
            foreach (var g in result.DuplicateGroups.Take(100))
            {
                sb.AppendLine("<h3>" + Html(FormatBytes(g.Size)) + " each; potential waste " + Html(FormatBytes(g.WastedBytes)) + "</h3><ul>");
                foreach (var f in g.Files) sb.AppendLine("<li>" + Html(f.Path) + "</li>");
                sb.AppendLine("</ul>");
            }
            AppendSimpleTable(sb, "Extensions", new[] { "Extension", "Count", "Size", "%" }, result.ExtensionStats.Select(s => new[] { s.Extension, s.Count.ToString(), FormatBytes(s.Bytes), s.PercentOfTotal.ToString("0.00") + "%" }));
            AppendSimpleTable(sb, "File age", new[] { "Age", "Count", "Size", "%" }, result.AgeStats.Select(s => new[] { s.Bucket, s.Count.ToString(), FormatBytes(s.Bytes), s.PercentOfTotal.ToString("0.00") + "%" }));
            AppendSimpleTable(sb, "Empty folders", new[] { "Path" }, result.EmptyFolders.Take(200).Select(f => new[] { f.Path }));
            if (result.SkippedItems.Count > 0) AppendSimpleTable(sb, "Skipped / access / filter log", new[] { "Item" }, result.SkippedItems.Take(300).Select(x => new[] { x }));
            sb.AppendLine("<p class='warn'>This report is read-only. The tool does not delete or modify user files.</p>");
            sb.AppendLine("</body></html>");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }

        private sealed class ChartItem { public string Label; public long Bytes; public double Percent; }

        private static void AppendBarChart(StringBuilder sb, string title, IEnumerable<ChartItem> items, string css)
        {
            var list = items.ToList();
            sb.AppendLine("<h2>" + Html(title) + "</h2>");
            if (list.Count == 0) { sb.AppendLine("<p>No data.</p>"); return; }
            long max = Math.Max(1, list.Max(i => i.Bytes));
            sb.AppendLine("<table><tr><th>Item</th><th>Size</th><th>%</th><th>Visual</th></tr>");
            foreach (var i in list)
            {
                int width = Math.Max(2, (int)Math.Round(100.0 * i.Bytes / max));
                sb.AppendLine("<tr><td>" + Html(i.Label) + "</td><td class='num'>" + Html(FormatBytes(i.Bytes)) + "</td><td class='num'>" + i.Percent.ToString("0.00") + "%</td><td><div class='bar " + css + "' style='width:" + width + "%;'>" + Html(FormatBytes(i.Bytes)) + "</div></td></tr>");
            }
            sb.AppendLine("</table>");
        }

        private static string JoinOrNone(IEnumerable<string> values)
        {
            var list = values == null ? new List<string>() : values.OrderBy(x => x).ToList();
            return list.Count == 0 ? "none" : String.Join(", ", list.ToArray());
        }

        private static string Card(string title, string value)
        {
            return "<div class='card'><b>" + Html(title) + "</b><br>" + Html(value) + "</div>";
        }

        private static void AppendFileTable(StringBuilder sb, string title, IEnumerable<FileRecord> files)
        {
            sb.AppendLine("<h2>" + Html(title) + "</h2><table><tr><th>Path</th><th>Extension</th><th>Size</th><th>Modified</th></tr>");
            foreach (var f in files) sb.AppendLine("<tr><td>" + Html(f.Path) + "</td><td>" + Html(f.Extension) + "</td><td class='num'>" + Html(FormatBytes(f.Size)) + "</td><td>" + Html(f.LastWriteTime.ToString("s")) + "</td></tr>");
            sb.AppendLine("</table>");
        }

        private static void AppendSimpleTable(StringBuilder sb, string title, string[] headers, IEnumerable<string[]> rows)
        {
            sb.AppendLine("<h2>" + Html(title) + "</h2><table><tr>" + String.Join("", headers.Select(h => "<th>" + Html(h) + "</th>")) + "</tr>");
            foreach (var row in rows) sb.AppendLine("<tr>" + String.Join("", row.Select(c => "<td>" + Html(c) + "</td>")) + "</tr>");
            sb.AppendLine("</table>");
        }

        public static string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double value = bytes;
            int unit = 0;
            while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
            return value.ToString(unit == 0 ? "0" : "0.##") + " " + units[unit];
        }

        public static string ShortPath(string root, string path)
        {
            if (String.IsNullOrEmpty(path)) return "";
            if (!String.IsNullOrEmpty(root) && path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                string rel = path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return rel.Length == 0 ? path : rel;
            }
            return path;
        }

        private static string Html(string text)
        {
            if (text == null) return "";
            return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }
    }

    public static class AppInfo
    {
        public const string ProductName = "ClearFolder Audit";
        public const string Version = "0.2.0";
    }
}

