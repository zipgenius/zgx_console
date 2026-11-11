param([string]$Msi)
$ErrorActionPreference = 'Stop'
$principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
if ($principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this verification from a non-elevated terminal.'
}
if (!$Msi) {
    $Msi = (Get-ChildItem "$PSScriptRoot/dist" -Filter 'zg-console-*-win-x64-user.msi' |
        Sort-Object { [version]($_.Name -replace '^zg-console-|-win-x64-user\.msi$', '') } -Descending |
        Select-Object -First 1).FullName
}
$Msi = (Resolve-Path -LiteralPath $Msi).Path
$target = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Programs/ZipGenius X Console'
if (Test-Path -LiteralPath $target) { throw "Refusing to replace an existing installation: $target" }
$work = Join-Path $PSScriptRoot 'work'
$userPathBefore = [Environment]::GetEnvironmentVariable('PATH', 'User')
$machinePathBefore = [Environment]::GetEnvironmentVariable('PATH', 'Machine')
$engine = New-Object -ComObject WindowsInstaller.Installer
if (!('ZgUserMsiState' -as [type])) {
    Add-Type @'
using System.Runtime.InteropServices;
public static class ZgUserMsiState {
    [DllImport("msi.dll", CharSet = CharSet.Unicode, EntryPoint = "MsiQueryProductStateW")]
    public static extern int Query(string product);
}
'@
}
function ReadMsiProperty($Database, [string]$Name) {
    $view = $Database.OpenView("SELECT ``Value`` FROM ``Property`` WHERE ``Property`` = '$Name'")
    [void]$view.Execute()
    $record = $view.Fetch()
    $value = $record.StringData(1)
    [void]$view.Close()
    [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($record)
    [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($view)
    [string]$value
}
$database = $engine.OpenDatabase($Msi, 0)
$originalCode = ReadMsiProperty $database 'ProductCode'
[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($database)
if ([ZgUserMsiState]::Query($originalCode) -gt 0) { throw 'The user MSI is already registered; refusing to replace it.' }
$upgradeCode = '{' + [guid]::NewGuid().ToString().ToUpperInvariant() + '}'
$fixture = Join-Path $work 'user-upgrade-test.msi'
Copy-Item -LiteralPath $Msi -Destination $fixture -Force
$database = $engine.OpenDatabase($fixture, 1)
$view = $database.OpenView("UPDATE ``Property`` SET ``Value`` = '$upgradeCode' WHERE ``Property`` = 'ProductCode'")
[void]$view.Execute()
[void]$view.Close()
[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($view)
$summary = $database.SummaryInformation(1)
$summary.Property(9) = '{' + [guid]::NewGuid().ToString().ToUpperInvariant() + '}'
$summary.Persist()
[void]$database.Commit()
[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($summary)
[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($database)
function MsiRun([string[]]$Arguments, [string]$LogName) {
    $log = Join-Path $work $LogName
    $process = Start-Process msiexec.exe -ArgumentList ($Arguments + @('/qn', '/norestart', '/l*v', "`"$log`"")) -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -notin 0, 3010) { throw "MSI failed: $($process.ExitCode); log: $log" }
}
function CheckInstalled {
    $exe = Join-Path $target 'zg.exe'
    if (!(Test-Path -LiteralPath $exe)) { throw 'User executable is missing.' }
    $path = [Environment]::GetEnvironmentVariable('PATH', 'User')
    $entries = @($path.Split(';') | Where-Object { $_.TrimEnd('\') -ieq $target.TrimEnd('\') })
    if ($entries.Count -ne 1) { throw 'Expected exactly one user PATH registration.' }
    if (![string]::Equals($machinePathBefore, [Environment]::GetEnvironmentVariable('PATH', 'Machine'), [StringComparison]::Ordinal)) {
        throw 'Machine PATH was changed.'
    }
    & $exe -help -info --silent | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Installed user application failed.' }
}
try {
    MsiRun @('/i', "`"$Msi`"") 'user-install.log'
    CheckInstalled
    MsiRun @('/i', "`"$Msi`"", 'REINSTALL=ALL', 'REINSTALLMODE=vomus') 'user-repair.log'
    CheckInstalled
    # A second product code exercises the real major-upgrade transaction while
    # preserving the published version and payload. The fixture is never distributed.
    MsiRun @('/i', "`"$fixture`"") 'user-upgrade.log'
    CheckInstalled
    if ([ZgUserMsiState]::Query($originalCode) -gt 0) { throw 'The preceding user product was not removed by upgrade.' }
    MsiRun @('/x', $upgradeCode) 'user-uninstall.log'
    if (Test-Path -LiteralPath $target) { throw 'User installation folder remains after removal.' }
    if (![string]::Equals($userPathBefore, [Environment]::GetEnvironmentVariable('PATH', 'User'), [StringComparison]::Ordinal)) {
        throw 'User PATH was not restored.'
    }
    if (![string]::Equals($machinePathBefore, [Environment]::GetEnvironmentVariable('PATH', 'Machine'), [StringComparison]::Ordinal)) {
        throw 'Machine PATH was changed.'
    }
    Write-Host 'PASS: non-elevated user MSI install, repair, major upgrade, removal and PATH restoration'
} finally {
    foreach ($code in @($upgradeCode, $originalCode)) {
        if ([ZgUserMsiState]::Query($code) -gt 0) {
            MsiRun @('/x', $code) 'user-cleanup.log'
        }
    }
}
