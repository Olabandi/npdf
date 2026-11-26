<#
.SYNOPSIS
  End of a work session: builds and signs the current Nero PDF package and puts a ZIP for testers
  into the shared OneDrive folder. Commit and push are done before (see .claude/skills/session-ende).

.DESCRIPTION
  The ZIP contains only what is needed to install and remove Nero PDF, no source code:
    - NeroPdf_<version>_x64.msix   the signed package
    - Kurzanleitung.txt            install, use and uninstall in short (docs/Kurzanleitung.txt)
  Without -Version, the last part of the highest version (ZIPs in the target folder, installed package) is increased.

.EXAMPLE
  .\build\session-end.ps1
  .\build\session-end.ps1 -Version 0.5.0.0
#>
param(
    [string]$Version,
    [string]$Target = (Join-Path $env:USERPROFILE "OneDrive\Stefan\Geschäftlich\Olabandi\Nero PDF"),
    [string]$CertificateName = "Nero AG",
    [switch]$NoInstall
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
New-Item -ItemType Directory -Force $Target | Out-Null

if (-not $Version) {
    $last = Get-ChildItem $Target -Filter "NeroPdf_*.zip" |
        ForEach-Object { if ($_.BaseName -match '^NeroPdf_(\d+\.\d+\.\d+\.\d+)$') { [version]$Matches[1] } } |
        Sort-Object | Select-Object -Last 1
    # The locally installed version counts too, so a new build never downgrades the installation.
    $installed = Get-AppxPackage -Name NeroPdf -ErrorAction SilentlyContinue | ForEach-Object { [version]$_.Version }
    $last = @($last, $installed) | Where-Object { $_ } | Sort-Object | Select-Object -Last 1
    if (-not $last) { $last = [version]"0.4.0.0" }
    $Version = "{0}.{1}.{2}.{3}" -f $last.Major, $last.Minor, $last.Build, ($last.Revision + 1)
}
Write-Host "Version $Version"

& (Join-Path $PSScriptRoot "package-windows.ps1") -CertificateName $CertificateName -Version $Version -Install:(-not $NoInstall)
$msix = Join-Path $root "artifacts\package\NeroPdf_$Version`_x64.msix"
if (-not (Test-Path $msix)) { throw "Package $msix was not built." }

$work = Join-Path ([IO.Path]::GetTempPath()) "NeroPdf_session_end"
Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory $work | Out-Null
Copy-Item $msix $work
$guide = [IO.File]::ReadAllText((Join-Path $root "docs\Kurzanleitung.txt")).Replace("__VERSION__", $Version)
[IO.File]::WriteAllText((Join-Path $work "Kurzanleitung.txt"), $guide, (New-Object Text.UTF8Encoding $true))

$zip = Join-Path $Target "NeroPdf_$Version.zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path (Join-Path $work "*") -DestinationPath $zip
Remove-Item $work -Recurse -Force
Write-Host "ZIP: $zip ($([math]::Round((Get-Item $zip).Length / 1MB, 1)) MB)"
