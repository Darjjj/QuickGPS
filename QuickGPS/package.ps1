# Builds the release zip: dist\QuickGPS-v<version>.zip, laid out to extract straight into the GTAIV folder.
# Usage: powershell -ExecutionPolicy Bypass -File package.ps1 [-GameDir "C:\...\GTAIV"]
param([string]$GameDir = "C:\Steam\steamapps\common\Grand Theft Auto IV\GTAIV")
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

& cmd /c "`"$root\build.cmd`" `"$GameDir`""
if ($LASTEXITCODE -ne 0) { throw "build failed" }

$dll = Join-Path $root "scripts\QuickGPS.net.dll"
$version = ([Reflection.AssemblyName]::GetAssemblyName($dll)).Version.ToString(3)

$dist = Join-Path $root "dist"
New-Item -ItemType Directory -Force $dist | Out-Null
$zip = Join-Path $dist "QuickGPS-v$version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }

# Written with System.IO.Compression so entry paths use "/" (Compress-Archive on PowerShell 5
# stores "scripts\file", which some unzip tools turn into a file name instead of a folder).
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$files = [ordered]@{
    "scripts/QuickGPS.net.dll" = $dll
    "scripts/QuickGPS.ini"     = (Join-Path $root "scripts\QuickGPS.ini")
    "QuickGPS-README.md"       = (Join-Path $root "..\README.md")
    "QuickGPS-LICENSE.txt"     = (Join-Path $root "..\LICENSE")
}
$archive = [IO.Compression.ZipFile]::Open($zip, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($name in $files.Keys) {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $files[$name], $name) | Out-Null
    }
} finally { $archive.Dispose() }
Write-Output $zip
