<#
.SYNOPSIS
  Removes Nero PDF completely: the app package, the virtual printer "Nero PDF" and its printer port.

.DESCRIPTION
  The normal way is Settings > Apps > Installed apps > Nero PDF > Uninstall; Windows then removes
  the printer together with the package. This script does the same and also cleans up what may be
  left over after an interrupted installation or uninstallation:
    1. Removes every installed Nero PDF package of the current user.
    2. Removes a remaining printer queue named "Nero PDF".
    3. Removes remaining printer ports of Nero PDF (needs "Run as administrator"; skipped otherwise).
  Nothing else is touched. Saved PDF files of the user stay where they are.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\uninstall-windows.ps1
#>
$ErrorActionPreference = "Continue"
$packageName = "NeroPdf"
$printerName = "Nero PDF"
$ports = "HKLM:\SYSTEM\CurrentControlSet\Control\Print\Monitors\Virtual Port Monitor\Ports"

Get-Process -Name $packageName -ErrorAction SilentlyContinue | Stop-Process -Force

$packages = @(Get-AppxPackage -Name $packageName)
foreach ($package in $packages) {
    Write-Host "Removing package $($package.PackageFullName) ..."
    Remove-AppxPackage -Package $package.PackageFullName
}
if ($packages.Count -eq 0) { Write-Host "No Nero PDF package installed." }

# Windows removes the virtual printer asynchronously after the package; give it a moment.
for ($i = 0; $i -lt 10 -and (Get-Printer -Name $printerName -ErrorAction SilentlyContinue); $i++) {
    Start-Sleep -Seconds 1
}
if (Get-Printer -Name $printerName -ErrorAction SilentlyContinue) {
    Write-Host "Removing leftover printer '$printerName' ..."
    Remove-Printer -Name $printerName -ErrorAction Continue
}

$leftoverPorts = @(Get-ChildItem $ports -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -like "$packageName`_*" })
if ($leftoverPorts.Count -gt 0) {
    $admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
    if ($admin) {
        foreach ($port in $leftoverPorts) {
            Write-Host "Removing leftover printer port $($port.PSChildName) ..."
            Remove-Item $port.PSPath -Recurse -Force
        }
        Write-Host "Please restart Windows to complete the removal of the printer port."
    }
    else {
        Write-Host "Leftover printer ports found. Run this script again 'as administrator' to remove them:"
        $leftoverPorts | ForEach-Object { Write-Host "  $($_.PSChildName)" }
    }
}

$remaining = (Get-AppxPackage -Name $packageName) -or (Get-Printer -Name $printerName -ErrorAction SilentlyContinue)
if ($remaining) { Write-Host "Nero PDF could not be removed completely, see the messages above." }
else { Write-Host "Nero PDF and its printer are removed." }
