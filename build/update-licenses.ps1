<#
.SYNOPSIS
  Collects the licence and notice texts of all third-party components that ship with Nero PDF
  into the folder "licenses". Run it again whenever a package version changes.

.DESCRIPTION
  - PDFium: LICENSE, VERSION and licenses/* of the matching pdfium-binaries release
    (the NuGet package itself contains no licence files).
  - NuGet packages that contain LICENSE / NOTICE / THIRD-PARTY-NOTICES files: copied from the
    local package cache.
  - Packages without licence files: licence text from the project's repository.
  The overview with versions, copyright holders and modifications is licenses/THIRD-PARTY-NOTICES.txt;
  it is maintained by hand and checked by a test.
#>
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$target = Join-Path $root "licenses"
$packages = Join-Path $env:USERPROFILE ".nuget\packages"
$versions = [xml](Get-Content (Join-Path $root "Directory.Packages.props") -Raw)

function PackageVersion([string]$id) {
    ($versions.Project.ItemGroup.PackageVersion | Where-Object { $_.Include -eq $id }).Version
}

function Get-Text([string]$url, [string]$file) {
    $dir = Split-Path -Parent $file
    New-Item -ItemType Directory -Force $dir | Out-Null
    Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $file
}

# PDFium from the pdfium-binaries release that matches the NuGet package (e.g. 160.0.8179 -> chromium/8179).
$pdfiumVersion = PackageVersion "bblanchon.PDFium.Win32"
$build = $pdfiumVersion.Split('.')[-1]
$work = Join-Path ([IO.Path]::GetTempPath()) "nero-pdfium-licenses"
Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $work | Out-Null
$archive = Join-Path $work "pdfium.tgz"
Invoke-WebRequest -UseBasicParsing -Uri "https://github.com/bblanchon/pdfium-binaries/releases/download/chromium%2F$build/pdfium-win-x64.tgz" -OutFile $archive
tar -xzf $archive -C $work LICENSE VERSION licenses
$pdfium = Join-Path $target "pdfium"
Remove-Item $pdfium -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $pdfium | Out-Null
Copy-Item (Join-Path $work "LICENSE") (Join-Path $pdfium "LICENSE.txt")
Copy-Item (Join-Path $work "VERSION") (Join-Path $pdfium "VERSION.txt")
Copy-Item (Join-Path $work "licenses\*") $pdfium
Set-Content (Join-Path $pdfium "PACKAGE-VERSION.txt") "bblanchon.PDFium $pdfiumVersion" -Encoding UTF8
Remove-Item $work -Recurse -Force

# Licence and notice files contained in NuGet packages (folder name, package id, version).
$fromPackages = @(
    @("skiasharp", "skiasharp", "3.119.4"),
    @("skiasharp", "skiasharp.nativeassets.win32", "3.119.4"),
    @("harfbuzzsharp", "harfbuzzsharp", "8.3.1.3"),
    @("harfbuzzsharp", "harfbuzzsharp.nativeassets.win32", "8.3.1.3"),
    @("angle", "avalonia.angle.windows.natives", "2.1.27548.20260419"),
    @("cswinrt", "microsoft.windows.cswinrt", (PackageVersion "Microsoft.Windows.CsWinRT")),
    @("microsoft-extensions", "microsoft.extensions.logging", (PackageVersion "Microsoft.Extensions.Logging"))
)
foreach ($entry in $fromPackages) {
    $source = Join-Path $packages "$($entry[1])\$($entry[2])"
    if (-not (Test-Path $source)) { throw "Package $($entry[1]) $($entry[2]) not in the local cache. Build once, then run again." }
    $dir = Join-Path $target $entry[0]
    New-Item -ItemType Directory -Force $dir | Out-Null
    Get-ChildItem $source -File | Where-Object { $_.Name -match '(?i)^(license|notice|third-party-notices)' } | ForEach-Object {
        $name = if ($_.Extension) { $_.BaseName } else { $_.Name }
        Copy-Item $_.FullName (Join-Path $dir ("$name-$($entry[1]).txt"))
    }
}

# Packages without licence files in the package: licence from the project repository.
Get-Text "https://raw.githubusercontent.com/AvaloniaUI/Avalonia/master/licence.md" (Join-Path $target "avalonia\LICENSE.txt")
Get-Text "https://raw.githubusercontent.com/kekekeks/MicroCom/master/LICENSE" (Join-Path $target "microcom\LICENSE.txt")
Get-Text "https://raw.githubusercontent.com/tmds/Tmds.DBus/main/COPYING" (Join-Path $target "tmds-dbus\LICENSE.txt")
Get-Text "https://raw.githubusercontent.com/cube-soft/cube.psa.samples/master/License.txt" (Join-Path $target "cube-psa-samples\LICENSE.txt")

Write-Host "Licence files updated in $target. Check THIRD-PARTY-NOTICES.txt for versions and changes."
