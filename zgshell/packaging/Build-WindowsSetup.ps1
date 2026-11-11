param([string]$MachineMsi, [string]$UserMsi)
$ErrorActionPreference = 'Stop'
$msiDirectories = @("$PSScriptRoot/dist", "$PSScriptRoot/work/windows-msi") | Where-Object { Test-Path -LiteralPath $_ }
if (!$MachineMsi) {
    $MachineMsi = (Get-ChildItem -LiteralPath $msiDirectories -Filter 'zg-console-*-win-x64.msi' |
        Sort-Object { [version]($_.Name -replace '^zg-console-|-win-x64\.msi$', '') } -Descending | Select-Object -First 1).FullName
}
if (!$UserMsi) {
    $UserMsi = (Get-ChildItem -LiteralPath $msiDirectories -Filter 'zg-console-*-win-x64-user.msi' |
        Sort-Object { [version]($_.Name -replace '^zg-console-|-win-x64-user\.msi$', '') } -Descending | Select-Object -First 1).FullName
}
$MachineMsi = (Resolve-Path -LiteralPath $MachineMsi).Path
$UserMsi = (Resolve-Path -LiteralPath $UserMsi).Path
$engine = New-Object -ComObject WindowsInstaller.Installer
function ReadProperty([string]$Path, [string]$Name) {
    $database = $engine.OpenDatabase($Path, 0)
    $view = $database.OpenView("SELECT ``Value`` FROM ``Property`` WHERE ``Property`` = '$Name'")
    [void]$view.Execute(); $record = $view.Fetch()
    $value = [string]$record.StringData(1)
    [void]$view.Close()
    foreach ($object in @($record, $view, $database)) { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($object) }
    $value
}
$version = ReadProperty $MachineMsi 'ProductVersion'
if ($version -ne (ReadProperty $UserMsi 'ProductVersion') -or $version -notmatch '^\d+\.\d+\.\d+\.\d+$') {
    throw 'Both MSI payloads must have the same four-field version.'
}
$machineProduct = ReadProperty $MachineMsi 'ProductCode'
$userProduct = ReadProperty $UserMsi 'ProductCode'
if ((ReadProperty $MachineMsi 'UpgradeCode') -ne '{7E0468AB-7C75-4D5E-9726-090E5D431A1A}' -or
    (ReadProperty $UserMsi 'UpgradeCode') -ne '{B69EC660-183F-42FA-9D05-0A63C6D61845}') { throw 'Unexpected MSI upgrade identities.' }
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$visualStudio = (& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath | Select-Object -First 1)
if (!$visualStudio) { throw 'Visual Studio C++ x64 build tools and Windows SDK are required.' }
$work = Join-Path $PSScriptRoot 'work/unified-setup'
New-Item -ItemType Directory -Path $work -Force | Out-Null
$header = "#pragma once`n#define SETUP_VERSION L`"$version`"`n#define SETUP_MACHINE_PRODUCT L`"$machineProduct`"`n#define SETUP_USER_PRODUCT L`"$userProduct`"`n"
[IO.File]::WriteAllText((Join-Path $work 'setup-config.h'), $header)
$resource = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'setup/setup.rc.in'))
$manifest = (Resolve-Path (Join-Path $PSScriptRoot 'setup/setup.manifest')).Path
$resource = $resource.Replace('@USER_MSI@', $UserMsi.Replace('\', '\\')).Replace('@MACHINE_MSI@', $MachineMsi.Replace('\', '\\')).Replace('@MANIFEST@', $manifest.Replace('\', '\\')).Replace('@VERSION@', $version).Replace('@NUMERIC_VERSION@', $version.Replace('.', ','))
[IO.File]::WriteAllText((Join-Path $work 'setup.rc'), $resource, [Text.UTF8Encoding]::new($false))
$output = Join-Path $PSScriptRoot "dist/zg-console-$version-win-x64-setup.exe"
$source = (Resolve-Path (Join-Path $PSScriptRoot 'setup/main.cpp')).Path
$devcmd = Join-Path $visualStudio 'Common7/Tools/VsDevCmd.bat'
$commands = @"
@echo off
call "$devcmd" -arch=x64 -host_arch=x64 >nul
if errorlevel 1 exit /b 1
rc /nologo /fo "$work/setup.res" "$work/setup.rc"
if errorlevel 1 exit /b 1
cl /nologo /std:c++20 /utf-8 /EHsc /W4 /MT /O2 /guard:cf /I "$work" /Fo"$work/setup.obj" /Fe"$output" "$source" "$work/setup.res" user32.lib gdi32.lib shell32.lib ole32.lib comctl32.lib msi.lib advapi32.lib /link /SUBSYSTEM:WINDOWS /MANIFEST:NO /DYNAMICBASE /NXCOMPAT /HIGHENTROPYVA
exit /b %errorlevel%
"@
$batch = Join-Path $work 'compile.cmd'
[IO.File]::WriteAllText($batch, $commands, [Text.Encoding]::ASCII)
& $env:ComSpec /d /c "`"$batch`""
if ($LASTEXITCODE -ne 0) { throw "Native setup build failed: $LASTEXITCODE" }
Get-ChildItem "$PSScriptRoot/dist" -File | Where-Object { $_.Name.EndsWith('.pkg.tar.zst') -or $_.Extension -in '.msi', '.exe', '.deb', '.rpm', '.snap', '.flatpak' } | Sort-Object Name | ForEach-Object {
    "$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant())  $($_.Name)"
} | Set-Content "$PSScriptRoot/dist/SHA256SUMS" -Encoding ascii
Write-Host "Unified setup: $output"
