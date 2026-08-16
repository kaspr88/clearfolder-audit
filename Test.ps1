$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src = Join-Path $root "src"
$testRoot = Join-Path $root "test-data"
$outRoot = Join-Path $testRoot "exports"
$loadRoot = Join-Path $testRoot "load-10000"
if (Test-Path $testRoot) { Remove-Item -Path $testRoot -Recurse -Force }
New-Item -ItemType Directory -Path $testRoot | Out-Null
New-Item -ItemType Directory -Path (Join-Path $testRoot "A") | Out-Null
New-Item -ItemType Directory -Path (Join-Path $testRoot "B") | Out-Null
New-Item -ItemType Directory -Path (Join-Path $testRoot "Empty") | Out-Null
New-Item -ItemType Directory -Path (Join-Path $testRoot "skipme") | Out-Null
Set-Content -Path (Join-Path $testRoot "A\duplicate-one.txt") -Value "same duplicate payload" -Encoding UTF8
Set-Content -Path (Join-Path $testRoot "B\duplicate-two.txt") -Value "same duplicate payload" -Encoding UTF8
Set-Content -Path (Join-Path $testRoot "A\unique.log") -Value ("x" * 4000) -Encoding ASCII
Set-Content -Path (Join-Path $testRoot "B\data.csv") -Value "a,b`n1,2" -Encoding UTF8
Set-Content -Path (Join-Path $testRoot "A\small.tmp") -Value "skip ext" -Encoding UTF8
Set-Content -Path (Join-Path $testRoot "skipme\ignored.txt") -Value "ignored folder" -Encoding UTF8
(Get-Item (Join-Path $testRoot "B\data.csv")).LastWriteTime = (Get-Date).AddDays(-45)
(Get-Item (Join-Path $testRoot "A\unique.log")).LastWriteTime = (Get-Date).AddDays(-400)
Add-Type -Path (Join-Path $src "FolderAudit.Core.cs") -ReferencedAssemblies @("System.Core")
$options = New-Object FolderAuditTool.AuditOptions
$options.ExcludedExtensions = [FolderAuditTool.AuditOptions]::ParseExtensions('.tmp')
$options.ExcludedFolderNames = [FolderAuditTool.AuditOptions]::ParseFolderNames('skipme')
$options.MinFileSizeBytes = 0
$result = [FolderAuditTool.FolderScanner]::Scan($testRoot, $options, $null, [Threading.CancellationToken]::None)
function Assert($condition, $message) { if (-not $condition) { throw "ASSERT FAILED: $message" } }
Assert ($result.FileCount -eq 4) "Expected 4 included files, got $($result.FileCount)"
Assert ($result.FolderCount -ge 4) "Expected at least 4 folders, got $($result.FolderCount)"
Assert ($result.TotalBytes -gt 4000) "Expected total bytes > 4000"
Assert ($result.DuplicateGroups.Count -eq 1) "Expected 1 duplicate group, got $($result.DuplicateGroups.Count)"
Assert ($result.DuplicateGroups[0].Files.Count -eq 2) "Expected duplicate group with 2 files"
Assert ((($result.EmptyFolders | Where-Object { $_.Path -like '*\Empty' }) | Measure-Object).Count -eq 1) "Expected Empty folder detected"
Assert ((($result.ExtensionStats | Where-Object { $_.Extension -eq '.txt' }) | Measure-Object).Count -eq 1) "Expected .txt extension stats"
Assert ((($result.AgeStats | Where-Object { $_.Bucket -eq 'Over 1 year' -and $_.Count -ge 1 }) | Measure-Object).Count -eq 1) "Expected over-1-year bucket"
Assert ($result.TopFolders.Count -gt 0) "Expected top folder stats"
Assert ($result.CleanupPlan.Count -gt 0) "Expected cleanup plan suggestions"
Assert ((($result.SkippedItems | Where-Object { $_ -like 'Excluded extension:*small.tmp*' }) | Measure-Object).Count -eq 1) "Expected excluded extension log"
Assert ((($result.SkippedItems | Where-Object { $_ -like 'Excluded folder:*skipme*' }) | Measure-Object).Count -eq 1) "Expected excluded folder log"
if (!(Test-Path $outRoot)) { New-Item -ItemType Directory -Path $outRoot | Out-Null }
[FolderAuditTool.ReportExporter]::ExportHtml($result, (Join-Path $outRoot "report.html"))
[FolderAuditTool.ReportExporter]::ExportCsv($result, $outRoot)
foreach($file in @('report.html','files.csv','largest_files.csv','top_folders.csv','duplicates.csv','empty_folders.csv','extensions.csv','age.csv','cleanup_plan.csv','skipped_items.txt')) { Assert (Test-Path (Join-Path $outRoot $file)) "Missing export $file" }
$html = Get-Content -Path (Join-Path $outRoot "report.html") -Raw -Encoding UTF8
Assert ($html.Contains('Top subfolders by size')) "HTML missing top folders chart"
Assert ($html.Contains('Distribution by extension')) "HTML missing extension chart"
Assert ($html.Contains('Distribution by file age')) "HTML missing age chart"
Assert ($html.Contains('Safe cleanup plan')) "HTML missing cleanup plan"
$cancelSource = New-Object System.Threading.CancellationTokenSource
$cancelSource.Cancel()
$cancelledOk = $false
try { [FolderAuditTool.FolderScanner]::Scan($testRoot, (New-Object FolderAuditTool.AuditOptions), $null, $cancelSource.Token) | Out-Null } catch [System.OperationCanceledException] { $cancelledOk = $true }
Assert $cancelledOk "Expected pre-cancelled scan to throw OperationCanceledException"
$lockDir = Join-Path $testRoot "locked"
New-Item -ItemType Directory -Path $lockDir | Out-Null
$lockedPath = Join-Path $lockDir "locked-a.bin"
$otherPath = Join-Path $lockDir "locked-b.bin"
[IO.File]::WriteAllBytes($lockedPath, [Text.Encoding]::UTF8.GetBytes('lock duplicate'))
[IO.File]::WriteAllBytes($otherPath, [Text.Encoding]::UTF8.GetBytes('lock duplicate'))
$stream = [IO.File]::Open($lockedPath, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
try {
  $accessResult = [FolderAuditTool.FolderScanner]::Scan($lockDir, (New-Object FolderAuditTool.AuditOptions), $null, [Threading.CancellationToken]::None)
  Assert ((($accessResult.SkippedItems | Where-Object { $_ -like 'Cannot hash file:*locked-a.bin*' }) | Measure-Object).Count -eq 1) "Expected locked file hash access error"
} finally { $stream.Close() }
New-Item -ItemType Directory -Path $loadRoot | Out-Null
for($d=0; $d -lt 100; $d++) {
  $sub = Join-Path $loadRoot ("group-{0:000}" -f $d)
  New-Item -ItemType Directory -Path $sub | Out-Null
  for($i=0; $i -lt 100; $i++) {
    $idx = $d * 100 + $i
    $ext = if($idx % 5 -eq 0) { '.txt' } elseif($idx % 5 -eq 1) { '.csv' } elseif($idx % 5 -eq 2) { '.json' } elseif($idx % 5 -eq 3) { '.bin' } else { '.md' }
    $content = "file $idx " + ('x' * (($idx % 37) + 1))
    [IO.File]::WriteAllText((Join-Path $sub ("file-{0:00000}{1}" -f $idx,$ext)), $content)
  }
}
$sw = [Diagnostics.Stopwatch]::StartNew()
$loadResult = [FolderAuditTool.FolderScanner]::Scan($loadRoot, (New-Object FolderAuditTool.AuditOptions), $null, [Threading.CancellationToken]::None)
$sw.Stop()
Assert ($loadResult.FileCount -eq 10000) "Expected 10000 files in load test, got $($loadResult.FileCount)"
Assert ($loadResult.TopFolders.Count -gt 0) "Expected top folders in load test"
$summary = @{
  FunctionalFiles = $result.FileCount
  FunctionalFolders = $result.FolderCount
  DuplicateGroups = $result.DuplicateGroups.Count
  EmptyFolders = $result.EmptyFolders.Count
  LoadFiles = $loadResult.FileCount
  LoadFolders = $loadResult.FolderCount
  LoadMilliseconds = $sw.ElapsedMilliseconds
  LoadSeconds = [Math]::Round($sw.Elapsed.TotalSeconds, 3)
}
$summaryPath = Join-Path $testRoot "test-summary.txt"
$summary.GetEnumerator() | Sort-Object Name | ForEach-Object { "$($_.Name): $($_.Value)" } | Set-Content -Path $summaryPath -Encoding UTF8
Write-Host "All v0.2 functional tests passed."
Write-Host "Load test: $($loadResult.FileCount) files, $($loadResult.FolderCount) folders, $($sw.ElapsedMilliseconds) ms."
Write-Host "Summary: $summaryPath"


