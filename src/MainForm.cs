using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FolderAuditTool
{
    public sealed class MainForm : Form
    {
        private TextBox folderTextBox;
        private TextBox excludeExtTextBox;
        private TextBox excludeFolderTextBox;
        private NumericUpDown minSizeNumeric;
        private ComboBox minSizeUnitBox;
        private Button browseButton;
        private Button scanButton;
        private Button cancelButton;
        private Button exportHtmlButton;
        private Button exportCsvButton;
        private Button aboutButton;
        private ProgressBar progressBar;
        private Label statusLabel;
        private Label summaryLabel;
        private TabControl tabs;
        private ListView foldersList;
        private ListView largestList;
        private ListView duplicatesList;
        private ListView emptyList;
        private ListView extensionsList;
        private ListView ageList;
        private ListView cleanupList;
        private TextBox logTextBox;
        private CancellationTokenSource cts;
        private AuditResult currentResult;

        public MainForm()
        {
            Text = AppInfo.ProductName + " " + AppInfo.Version;
            Width = 1180;
            Height = 820;
            MinimumSize = new Size(980, 650);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);
            Icon = BuildIcon();
            BuildUi();
            SetScanning(false);
        }

        private void BuildUi()
        {
            TableLayoutPanel root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            Controls.Add(root);

            FlowLayoutPanel top = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10, 10, 10, 6), WrapContents = false, AutoScroll = true };
            root.Controls.Add(top, 0, 0);
            top.Controls.Add(new Label { Text = "Folder:", AutoSize = true, Padding = new Padding(0, 7, 2, 0) });
            folderTextBox = new TextBox { Width = 650 };
            top.Controls.Add(folderTextBox);
            browseButton = new Button { Text = "Browse...", Width = 90 };
            browseButton.Click += BrowseButton_Click;
            top.Controls.Add(browseButton);
            scanButton = new Button { Text = "Scan", Width = 90 };
            scanButton.Click += ScanButton_Click;
            top.Controls.Add(scanButton);
            cancelButton = new Button { Text = "Cancel", Width = 90 };
            cancelButton.Click += CancelButton_Click;
            top.Controls.Add(cancelButton);
            aboutButton = new Button { Text = "About", Width = 80 };
            aboutButton.Click += AboutButton_Click;
            top.Controls.Add(aboutButton);

            GroupBox filters = new GroupBox { Text = "Filters (optional, read-only scan)", Dock = DockStyle.Fill, Padding = new Padding(10, 4, 10, 6) };
            root.Controls.Add(filters, 0, 1);
            TableLayoutPanel filterGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 1 };
            filterGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135));
            filterGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            filterGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            filterGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            filterGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
            filterGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            filters.Controls.Add(filterGrid);
            filterGrid.Controls.Add(new Label { Text = "Exclude extensions:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 0);
            excludeExtTextBox = new TextBox { Dock = DockStyle.Fill, Text = ".tmp,.log" };
            filterGrid.Controls.Add(excludeExtTextBox, 1, 0);
            filterGrid.Controls.Add(new Label { Text = "Exclude folders:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 2, 0);
            excludeFolderTextBox = new TextBox { Dock = DockStyle.Fill, Text = "node_modules,.git" };
            filterGrid.Controls.Add(excludeFolderTextBox, 3, 0);
            filterGrid.Controls.Add(new Label { Text = "Min file size:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 4, 0);
            FlowLayoutPanel minPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            minSizeNumeric = new NumericUpDown { Minimum = 0, Maximum = 999999, Value = 0, Width = 85 };
            minSizeUnitBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 70 };
            minSizeUnitBox.Items.AddRange(new object[] { "B", "KB", "MB", "GB" });
            minSizeUnitBox.SelectedIndex = 0;
            minPanel.Controls.Add(minSizeNumeric);
            minPanel.Controls.Add(minSizeUnitBox);
            filterGrid.Controls.Add(minPanel, 5, 0);

            FlowLayoutPanel exports = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10, 4, 10, 4), WrapContents = false };
            root.Controls.Add(exports, 0, 2);
            exportHtmlButton = new Button { Text = "Export HTML report", Width = 150 };
            exportHtmlButton.Click += ExportHtmlButton_Click;
            exports.Controls.Add(exportHtmlButton);
            exportCsvButton = new Button { Text = "Export CSV files", Width = 140 };
            exportCsvButton.Click += ExportCsvButton_Click;
            exports.Controls.Add(exportCsvButton);
            exports.Controls.Add(new Label { Text = "Tip: right-click a result to open it in Explorer or copy its path.", AutoSize = true, Padding = new Padding(12, 8, 0, 0), ForeColor = Color.DarkGreen });

            Panel summaryPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 0, 10, 0) };
            root.Controls.Add(summaryPanel, 0, 3);
            summaryLabel = new Label { Dock = DockStyle.Fill, Text = "Choose a folder and click Scan.", AutoEllipsis = true };
            progressBar = new ProgressBar { Dock = DockStyle.Bottom, Height = 18, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 0 };
            summaryPanel.Controls.Add(summaryLabel);
            summaryPanel.Controls.Add(progressBar);

            tabs = new TabControl { Dock = DockStyle.Fill };
            root.Controls.Add(tabs, 0, 4);
            foldersList = AddTab("Top folders", new[] { "%", "Size", "Files", "Subfolders", "Path" });
            largestList = AddTab("Largest files", new[] { "Size", "Extension", "Modified", "Path" });
            duplicatesList = AddTab("Duplicate groups", new[] { "Group", "Wasted", "Size each", "Files", "Path" });
            emptyList = AddTab("Empty folders", new[] { "Path" });
            extensionsList = AddTab("Extensions", new[] { "%", "Extension", "Files", "Size" });
            ageList = AddTab("Age", new[] { "%", "Age bucket", "Files", "Size" });
            cleanupList = AddTab("Safe cleanup plan", new[] { "Priority", "Category", "Size", "Reason", "Path" });

            TabPage logPage = new TabPage("Log");
            logTextBox = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Both, ReadOnly = true, Font = new Font("Consolas", 9F) };
            logPage.Controls.Add(logTextBox);
            tabs.TabPages.Add(logPage);

            statusLabel = new Label { Dock = DockStyle.Fill, Text = "Ready", BorderStyle = BorderStyle.Fixed3D, Padding = new Padding(6, 4, 0, 0) };
            root.Controls.Add(statusLabel, 0, 5);
        }

        private ListView AddTab(string title, string[] columns)
        {
            TabPage page = new TabPage(title);
            ListView lv = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, HideSelection = false };
            foreach (string c in columns) lv.Columns.Add(c, c == "Path" ? 680 : c == "Group" ? 65 : 105);
            lv.ColumnClick += ListView_ColumnClick;
            lv.MouseDoubleClick += ListView_MouseDoubleClick;
            lv.ContextMenuStrip = BuildResultMenu(lv);
            page.Controls.Add(lv);
            tabs.TabPages.Add(page);
            return lv;
        }

        private ContextMenuStrip BuildResultMenu(ListView lv)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("Open in Explorer", null, (s, e) => OpenSelectedInExplorer(lv, false));
            menu.Items.Add("Select in Explorer", null, (s, e) => OpenSelectedInExplorer(lv, true));
            menu.Items.Add("Copy path", null, (s, e) => CopySelectedPath(lv));
            return menu;
        }

        private void BrowseButton_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Select a folder to audit";
                dlg.ShowNewFolderButton = false;
                if (Directory.Exists(folderTextBox.Text)) dlg.SelectedPath = folderTextBox.Text;
                if (dlg.ShowDialog(this) == DialogResult.OK) folderTextBox.Text = dlg.SelectedPath;
            }
        }

        private void ScanButton_Click(object sender, EventArgs e)
        {
            string folder = folderTextBox.Text.Trim();
            if (!Directory.Exists(folder))
            {
                MessageBox.Show(this, "Please choose an existing folder.", AppInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ClearResults();
            SetScanning(true);
            cts = new CancellationTokenSource();
            var progress = new Progress<ScanProgress>(UpdateProgress);
            statusLabel.Text = "Scanning...";
            AuditOptions options = BuildOptions();

            Task.Factory.StartNew(() => FolderScanner.Scan(folder, options, progress, cts.Token), cts.Token)
                .ContinueWith(t =>
                {
                    BeginInvoke((Action)(() =>
                    {
                        SetScanning(false);
                        if (t.IsCanceled || (t.IsFaulted && t.Exception != null && t.Exception.GetBaseException() is OperationCanceledException))
                        {
                            statusLabel.Text = "Scan cancelled.";
                            summaryLabel.Text = "Scan cancelled. No files were changed.";
                        }
                        else if (t.IsFaulted)
                        {
                            statusLabel.Text = "Scan failed.";
                            MessageBox.Show(this, t.Exception.GetBaseException().Message, "Scan failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        else
                        {
                            currentResult = t.Result;
                            PopulateResults(currentResult);
                            statusLabel.Text = "Scan complete.";
                        }
                    }));
                });
        }

        private AuditOptions BuildOptions()
        {
            AuditOptions options = new AuditOptions();
            options.ExcludedExtensions = AuditOptions.ParseExtensions(excludeExtTextBox.Text);
            options.ExcludedFolderNames = AuditOptions.ParseFolderNames(excludeFolderTextBox.Text);
            decimal amount = minSizeNumeric.Value;
            string unit = minSizeUnitBox.SelectedItem == null ? "B" : minSizeUnitBox.SelectedItem.ToString();
            long multiplier = unit == "KB" ? 1024L : unit == "MB" ? 1024L * 1024L : unit == "GB" ? 1024L * 1024L * 1024L : 1L;
            options.MinFileSizeBytes = (long)(amount * multiplier);
            return options;
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            if (cts != null) cts.Cancel();
            statusLabel.Text = "Cancelling...";
        }

        private void AboutButton_Click(object sender, EventArgs e)
        {
            MessageBox.Show(this, AppInfo.ProductName + " " + AppInfo.Version + Environment.NewLine + Environment.NewLine + "Read-only folder audit: size, duplicates, empty folders, extensions, age, filters, HTML/CSV export." + Environment.NewLine + Environment.NewLine + "This app does not delete, move, rename, upload, or modify files.", "About " + AppInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void UpdateProgress(ScanProgress p)
        {
            summaryLabel.Text = String.Format("{0} | Files: {1:n0} | Folders: {2:n0} | Size: {3} | Current: {4}", p.Phase, p.FilesScanned, p.FoldersScanned, ReportExporter.FormatBytes(p.BytesScanned), TruncateMiddle(p.CurrentPath, 105));
        }

        private void PopulateResults(AuditResult r)
        {
            summaryLabel.Text = String.Format("Total: {0} | Files: {1:n0} | Folders: {2:n0} | Top folders: {3:n0} | Duplicate groups: {4:n0} | Potential duplicate waste: {5} | Empty folders: {6:n0}", ReportExporter.FormatBytes(r.TotalBytes), r.FileCount, r.FolderCount, r.TopFolders.Count, r.DuplicateGroups.Count, ReportExporter.FormatBytes(r.DuplicateWastedBytes), r.EmptyFolders.Count);

            foreach (var s in r.TopFolders) foldersList.Items.Add(Item(new[] { s.PercentOfTotal.ToString("0.00") + "%", ReportExporter.FormatBytes(s.Bytes), s.FileCount.ToString("n0"), s.FolderCount.ToString("n0"), s.Path }, s.Path, s.Bytes, s.PercentOfTotal));
            foreach (var f in r.LargestFiles) largestList.Items.Add(Item(new[] { ReportExporter.FormatBytes(f.Size), f.Extension, f.LastWriteTime.ToString("yyyy-MM-dd HH:mm"), f.Path }, f.Path, f.Size, 0));
            int groupNo = 1;
            foreach (var g in r.DuplicateGroups)
            {
                foreach (var f in g.Files) duplicatesList.Items.Add(Item(new[] { groupNo.ToString(), ReportExporter.FormatBytes(g.WastedBytes), ReportExporter.FormatBytes(g.Size), g.Files.Count.ToString(), f.Path }, f.Path, g.WastedBytes, 0));
                groupNo++;
            }
            foreach (var f in r.EmptyFolders) emptyList.Items.Add(Item(new[] { f.Path }, f.Path, 0, 0));
            foreach (var s in r.ExtensionStats) extensionsList.Items.Add(Item(new[] { s.PercentOfTotal.ToString("0.00") + "%", s.Extension, s.Count.ToString("n0"), ReportExporter.FormatBytes(s.Bytes) }, s.Extension, s.Bytes, s.PercentOfTotal));
            foreach (var s in r.AgeStats) ageList.Items.Add(Item(new[] { s.PercentOfTotal.ToString("0.00") + "%", s.Bucket, s.Count.ToString("n0"), ReportExporter.FormatBytes(s.Bytes) }, s.Bucket, s.Bytes, s.PercentOfTotal));
            foreach (var s in r.CleanupPlan) cleanupList.Items.Add(Item(new[] { s.Priority.ToString(), s.Category, ReportExporter.FormatBytes(s.Bytes), s.Reason, s.Path }, s.Path, s.Bytes, 0));
            logTextBox.Text = r.SkippedItems.Count == 0 ? "No skipped files, excluded items, or access errors." : String.Join(Environment.NewLine, r.SkippedItems.ToArray());
            AutoResizeAll();
        }

        private ListViewItem Item(string[] values, string path, long sortBytes, double sortPercent)
        {
            ListViewItem item = new ListViewItem(values);
            item.Tag = new ResultTag { Path = path, SortBytes = sortBytes, SortPercent = sortPercent };
            return item;
        }

        private void ExportHtmlButton_Click(object sender, EventArgs e)
        {
            if (currentResult == null) return;
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Filter = "HTML report (*.html)|*.html";
                dlg.FileName = "folder-audit-report.html";
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    ReportExporter.ExportHtml(currentResult, dlg.FileName);
                    if (MessageBox.Show(this, "HTML report exported. Open it now?", "Export complete", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes) Process.Start(dlg.FileName);
                }
            }
        }

        private void ExportCsvButton_Click(object sender, EventArgs e)
        {
            if (currentResult == null) return;
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Choose a folder for CSV export files";
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    ReportExporter.ExportCsv(currentResult, dlg.SelectedPath);
                    MessageBox.Show(this, "CSV export complete. Files: files.csv, largest_files.csv, top_folders.csv, duplicates.csv, empty_folders.csv, extensions.csv, age.csv, skipped_items.txt", "Export complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void ListView_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            OpenSelectedInExplorer((ListView)sender, true);
        }

        private void ListView_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            ListView lv = (ListView)sender;
            ListViewItemSorter sorter = lv.ListViewItemSorter as ListViewItemSorter;
            if (sorter == null || sorter.Column != e.Column) sorter = new ListViewItemSorter(e.Column, true);
            else sorter.Ascending = !sorter.Ascending;
            lv.ListViewItemSorter = sorter;
            lv.Sort();
        }

        private void OpenSelectedInExplorer(ListView lv, bool selectFile)
        {
            string path = SelectedPath(lv);
            if (String.IsNullOrEmpty(path)) return;
            try
            {
                if (File.Exists(path) && selectFile) Process.Start("explorer.exe", "/select,\"" + path + "\"");
                else if (File.Exists(path)) Process.Start("explorer.exe", "\"" + Path.GetDirectoryName(path) + "\"");
                else if (Directory.Exists(path)) Process.Start("explorer.exe", "\"" + path + "\"");
                else MessageBox.Show(this, "Path no longer exists: " + path, "Open in Explorer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Open in Explorer failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void CopySelectedPath(ListView lv)
        {
            string path = SelectedPath(lv);
            if (!String.IsNullOrEmpty(path)) Clipboard.SetText(path);
        }

        private string SelectedPath(ListView lv)
        {
            if (lv.SelectedItems.Count == 0) return null;
            ResultTag tag = lv.SelectedItems[0].Tag as ResultTag;
            return tag == null ? null : tag.Path;
        }

        private void SetScanning(bool scanning)
        {
            scanButton.Enabled = !scanning;
            browseButton.Enabled = !scanning;
            folderTextBox.Enabled = !scanning;
            excludeExtTextBox.Enabled = !scanning;
            excludeFolderTextBox.Enabled = !scanning;
            minSizeNumeric.Enabled = !scanning;
            minSizeUnitBox.Enabled = !scanning;
            cancelButton.Enabled = scanning;
            exportHtmlButton.Enabled = !scanning && currentResult != null;
            exportCsvButton.Enabled = !scanning && currentResult != null;
            progressBar.MarqueeAnimationSpeed = scanning ? 30 : 0;
        }

        private void ClearResults()
        {
            currentResult = null;
            foreach (ListView lv in new[] { foldersList, largestList, duplicatesList, emptyList, extensionsList, ageList, cleanupList }) lv.Items.Clear();
            logTextBox.Clear();
        }

        private void AutoResizeAll()
        {
            foreach (ListView lv in new[] { foldersList, largestList, duplicatesList, emptyList, extensionsList, ageList, cleanupList })
                foreach (ColumnHeader c in lv.Columns) c.Width = c.Text == "Path" ? 680 : -2;
        }

        private Icon BuildIcon()
        {
            Bitmap bmp = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(45, 111, 220))) g.FillRectangle(b, 4, 10, 24, 17);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(92, 158, 255))) g.FillRectangle(b, 4, 7, 11, 6);
                using (Pen p = new Pen(Color.White, 2)) { g.DrawLine(p, 9, 19, 15, 24); g.DrawLine(p, 15, 24, 24, 13); }
                using (Pen p = new Pen(Color.FromArgb(30, 70, 140), 1)) g.DrawRectangle(p, 4, 10, 24, 17);
            }
            return Icon.FromHandle(bmp.GetHicon());
        }

        private static string TruncateMiddle(string value, int max)
        {
            if (String.IsNullOrEmpty(value) || value.Length <= max) return value ?? "";
            int left = max / 2 - 2;
            int right = max - left - 3;
            return value.Substring(0, left) + "..." + value.Substring(value.Length - right);
        }

        private sealed class ResultTag { public string Path; public long SortBytes; public double SortPercent; }

        private sealed class ListViewItemSorter : IComparer
        {
            public int Column { get; private set; }
            public bool Ascending { get; set; }
            public ListViewItemSorter(int column, bool ascending) { Column = column; Ascending = ascending; }
            public int Compare(object x, object y)
            {
                ListViewItem a = (ListViewItem)x;
                ListViewItem b = (ListViewItem)y;
                string av = a.SubItems.Count > Column ? a.SubItems[Column].Text : "";
                string bv = b.SubItems.Count > Column ? b.SubItems[Column].Text : "";
                double ad, bd;
                int result;
                if (TryParsePercent(av, out ad) && TryParsePercent(bv, out bd)) result = ad.CompareTo(bd);
                else if (TryParseHumanBytes(av, out ad) && TryParseHumanBytes(bv, out bd)) result = ad.CompareTo(bd);
                else result = String.Compare(av, bv, StringComparison.CurrentCultureIgnoreCase);
                return Ascending ? result : -result;
            }
            private static bool TryParsePercent(string s, out double value)
            {
                s = (s ?? "").Replace("%", "").Trim();
                return Double.TryParse(s, out value);
            }
            private static bool TryParseHumanBytes(string s, out double value)
            {
                value = 0;
                if (String.IsNullOrWhiteSpace(s)) return false;
                string[] parts = s.Split(' ');
                double n;
                if (!Double.TryParse(parts[0], out n)) return false;
                string u = parts.Length > 1 ? parts[1].ToUpperInvariant() : "B";
                double m = u == "KB" ? 1024 : u == "MB" ? 1024 * 1024 : u == "GB" ? 1024 * 1024 * 1024 : u == "TB" ? 1024.0 * 1024 * 1024 * 1024 : 1;
                value = n * m;
                return true;
            }
        }
    }
}

