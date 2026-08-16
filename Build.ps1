param(
    [string]$Configuration = "Release"
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src = Join-Path $root "src"
$dist = Join-Path $root "dist"
if (!(Test-Path $dist)) { New-Item -ItemType Directory -Path $dist -Force | Out-Null }
$out = Join-Path $dist "FolderAuditTool.exe"
if (Test-Path $out) { Remove-Item -Path $out -Force }
$files = @(
    (Join-Path $src "FolderAudit.Core.cs"),
    (Join-Path $src "MainForm.cs"),
    (Join-Path $src "Program.cs")
)
Add-Type -Path $files -ReferencedAssemblies @("System.Windows.Forms", "System.Drawing", "System.Core") -OutputAssembly $out -OutputType WindowsApplication
Write-Host "Built: $out"
Get-Item $out | Select-Object FullName,Length,LastWriteTime
