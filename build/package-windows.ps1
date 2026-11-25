<#
.SYNOPSIS
  Builds the Nero PDF MSIX package (app + virtual printer) for Windows 11 24H2 and later.

.DESCRIPTION
  1. Publishes NeroPdf.Windows and builds the print tasks (x64).
  2. Puts both into one package layout together with the manifest, logos and printer capabilities.
  3. Generates the package resources (app and printer name) from the language files.
  4. Packs, signs and timestamps the package. Certificates never live in the repository: the code
     signing certificate is taken from the current user's certificate store, by subject name
     (e.g. "Nero AG", as in the other Nero projects) or by thumbprint.

.EXAMPLE
  .\build\package-windows.ps1 -CertificateName "Nero AG" -Version 0.1.0.0 -Install
#>
param(
    [string]$CertificateName,
    [string]$CertificateThumbprint,
    [string]$TimestampUrl = "http://timestamp.globalsign.com/tsa/r6advanced1",
    [string]$Version = "0.1.0.0",
    [string]$Configuration = "Release",
    [switch]$Install
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root "artifacts\package"
$layout = Join-Path $out "layout"
$resources = Join-Path $out "resources"
$msix = Join-Path $out "NeroPdf_$Version`_x64.msix"

$sdkBin = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Directory |
    Where-Object { $_.Name -match '^10\.' -and (Test-Path (Join-Path $_.FullName "x64\makeappx.exe")) } |
    Sort-Object { [version]$_.Name } | Select-Object -Last 1
if (-not $sdkBin) { throw "Windows SDK with makeappx.exe not found." }
$tools = Join-Path $sdkBin.FullName "x64"

if ($CertificateThumbprint) {
    $cert = Get-Item "Cert:\CurrentUser\My\$CertificateThumbprint"
}
elseif ($CertificateName) {
    $cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert |
        Where-Object { ($_.Subject -like "CN=$CertificateName,*" -or $_.Subject -eq "CN=$CertificateName") -and $_.NotAfter -gt (Get-Date) } |
        Sort-Object NotAfter | Select-Object -Last 1
    if (-not $cert) { throw "No valid code signing certificate for '$CertificateName' in the current user's store." }
}
else { throw "Pass -CertificateName or -CertificateThumbprint." }
Write-Host "Signing certificate: $($cert.Subject)"

Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $layout, $resources | Out-Null

Write-Host "Publishing app ..."
dotnet publish (Join-Path $root "src\NeroPdf.Windows\NeroPdf.Windows.csproj") -c $Configuration -o $layout --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

Write-Host "Building print tasks ..."
$tasksOut = Join-Path $out "tasks"
dotnet build (Join-Path $root "src\NeroPdf.Windows.PrintTasks\NeroPdf.Windows.PrintTasks.csproj") -c $Configuration -o $tasksOut --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw "Building the print tasks failed." }
# The print tasks need WinRT.Host.dll and their own files; shared libraries come from the app.
Get-ChildItem $tasksOut -File | Where-Object { -not (Test-Path (Join-Path $layout $_.Name)) -or $_.Name -like "WinRT.*" } |
    Copy-Item -Destination $layout -Force

Write-Host "Adding manifest, logos and printer capabilities ..."
Copy-Item (Join-Path $root "packaging\windows\Assets") $layout -Recurse -Force
Copy-Item (Join-Path $root "packaging\windows\Config") $layout -Recurse -Force

# Package resources (app and printer name) from the language files, one .resw per language.
$languages = Get-ChildItem (Join-Path $root "src\NeroPdf.App\Languages") -Filter *.json | Sort-Object Name
$resourceLines = @()
foreach ($file in $languages) {
    # MSIX does not accept a bare "zh"; the file zh.json holds Simplified Chinese.
    $language = if ($file.BaseName -eq "zh") { "zh-Hans" } else { $file.BaseName }
    $texts = Get-Content $file.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    $dir = Join-Path $resources "Strings\$language"
    New-Item -ItemType Directory -Force $dir | Out-Null
    $resw = @"
<?xml version="1.0" encoding="utf-8"?>
<root>
  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
  <resheader name="version"><value>2.0</value></resheader>
  <resheader name="reader"><value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
  <resheader name="writer"><value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
  <data name="AppName" xml:space="preserve"><value>$([Security.SecurityElement]::Escape($texts.'App.Name'))</value></data>
  <data name="PrinterName" xml:space="preserve"><value>$([Security.SecurityElement]::Escape($texts.'Printer.Name'))</value></data>
</root>
"@
    [IO.File]::WriteAllText((Join-Path $dir "Resources.resw"), $resw, (New-Object Text.UTF8Encoding $false))
    $resourceLines += "    <Resource Language=`"$language`" />"
}

$manifest = Get-Content (Join-Path $root "packaging\windows\AppxManifest.template.xml") -Raw -Encoding UTF8
$manifest = $manifest.Replace("__VERSION__", $Version).Replace("__PUBLISHER__", [Security.SecurityElement]::Escape($cert.Subject)).Replace("__RESOURCES__", ($resourceLines -join "`r`n"))
[IO.File]::WriteAllText((Join-Path $layout "AppxManifest.xml"), $manifest, (New-Object Text.UTF8Encoding $false))

Write-Host "Generating resources.pri ..."
Push-Location $resources
try {
    & (Join-Path $tools "makepri.exe") createconfig /cf priconfig.xml /dq en /o | Out-Null
    & (Join-Path $tools "makepri.exe") new /pr $resources /cf priconfig.xml /mn (Join-Path $layout "AppxManifest.xml") /of (Join-Path $layout "resources.pri") /o | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "makepri failed." }
}
finally { Pop-Location }

Write-Host "Packing and signing ..."
& (Join-Path $tools "makeappx.exe") pack /d $layout /p $msix /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw "makeappx failed." }
$timestamp = if ($TimestampUrl) { @("/tr", $TimestampUrl, "/td", "SHA256") } else { @() }
& (Join-Path $tools "signtool.exe") sign /fd SHA256 @timestamp /sha1 $cert.Thumbprint /s My $msix | Out-Null
if ($LASTEXITCODE -ne 0) { throw "signtool failed." }
Write-Host "Package: $msix"

if ($Install) {
    Write-Host "Installing ..."
    Add-AppxPackage -Path $msix -ForceUpdateFromAnyVersion -ForceApplicationShutdown
    Write-Host "Installed: $((Get-AppxPackage -Name NeroPdf).Version)"
}
