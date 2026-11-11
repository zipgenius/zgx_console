param([string]$Setup, [string]$UserMsi)
$ErrorActionPreference = 'Stop'
$principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
if ($principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Run from a non-elevated terminal.' }
if (!$Setup) { $Setup = (Get-ChildItem "$PSScriptRoot/dist" -Filter '*-win-x64-setup.exe' | Sort-Object { [version]$_.VersionInfo.ProductVersion } -Descending | Select-Object -First 1).FullName }
$Setup = (Resolve-Path -LiteralPath $Setup).Path
$version = (Get-Item -LiteralPath $Setup).VersionInfo.ProductVersion
if (!$UserMsi) {
    $UserMsi = @("$PSScriptRoot/work/windows-msi/zg-console-$version-win-x64-user.msi", "$PSScriptRoot/dist/zg-console-$version-win-x64-user.msi") | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
$UserMsi = (Resolve-Path -LiteralPath $UserMsi).Path
$target = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Programs/ZipGenius X Console'
if (Test-Path -LiteralPath $target) { throw 'Refusing to replace an existing installation.' }
$beforeUser = [Environment]::GetEnvironmentVariable('PATH', 'User')
$beforeMachine = [Environment]::GetEnvironmentVariable('PATH', 'Machine')
$work = Join-Path $PSScriptRoot 'work/unified-setup'
$engine = New-Object -ComObject WindowsInstaller.Installer
function Property([string]$Path, [string]$Name) {
    $db = $engine.OpenDatabase($Path, 0)
    $view = $db.OpenView("SELECT ``Value`` FROM ``Property`` WHERE ``Property`` = '$Name'")
    [void]$view.Execute(); $record = $view.Fetch(); $value = [string]$record.StringData(1)
    [void]$view.Close()
    foreach ($object in @($record, $view, $db)) { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($object) }
    $value
}
if ((Property $UserMsi 'ProductVersion') -ne $version) { throw 'Setup and MSI versions differ.' }
$product = Property $UserMsi 'ProductCode'
if (!('ZgUnifiedMsiState' -as [type])) {
    Add-Type @'
using System.Runtime.InteropServices;
public static class ZgUnifiedMsiState {
    [DllImport("msi.dll", CharSet=CharSet.Unicode, EntryPoint="MsiQueryProductStateW")]
    public static extern int Query(string code);
}
'@
}
if ([ZgUnifiedMsiState]::Query($product) -gt 0) { throw 'User product already registered.' }
function Launch([string[]]$Arguments, [int]$Expected = 0) {
    $process = Start-Process -FilePath $Setup -ArgumentList $Arguments -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne $Expected) { throw "Setup returned $($process.ExitCode), expected $Expected. Arguments: $Arguments" }
}
function Plan([string]$Name, [string[]]$Extra = @()) {
    $path = Join-Path $work "$Name.json"
    Launch (@('/quiet', '/plan', "`"$path`"") + $Extra)
    Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}
function CheckInstalled {
    $plan = Plan 'installed'
    if ($plan.scope -ne 'user' -or !$plan.installed -or $plan.elevationRequired) { throw 'The existing user scope was not preserved.' }
    if (!(Test-Path -LiteralPath "$target/zg.exe")) { throw 'User executable missing.' }
    if (@([Environment]::GetEnvironmentVariable('PATH','User').Split(';') | Where-Object { $_.TrimEnd('\') -ieq $target.TrimEnd('\') }).Count -ne 1) { throw 'Invalid user PATH registration.' }
    if (![string]::Equals($beforeMachine, [Environment]::GetEnvironmentVariable('PATH','Machine'), [StringComparison]::Ordinal)) { throw 'Machine PATH changed.' }
    & "$target/zg.exe" -help -info --silent | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Installed console failed.' }
}
function CheckRemoved {
    if (Test-Path -LiteralPath $target) { throw 'User folder remains after removal.' }
    if (![string]::Equals($beforeUser, [Environment]::GetEnvironmentVariable('PATH','User'), [StringComparison]::Ordinal)) { throw 'User PATH was not restored.' }
    if (![string]::Equals($beforeMachine, [Environment]::GetEnvironmentVariable('PATH','Machine'), [StringComparison]::Ordinal)) { throw 'Machine PATH changed.' }
}
$legacyCode = '{' + [guid]::NewGuid().ToString().ToUpperInvariant() + '}'
try {
    $fresh = Plan 'fresh'
    if ($fresh.scope -ne 'user' -or $fresh.installed -or $fresh.elevationRequired) { throw 'Fresh setup must default to the user scope.' }
    $machine = Plan 'machine' @('/allusers')
    if ($machine.scope -ne 'machine' -or !$machine.elevationRequired) { throw 'Machine setup must request elevation.' }
    Launch @('/quiet')
    CheckInstalled
    Launch @('/quiet', '/repair')
    CheckInstalled
    # An opposite-scope request must be rejected before elevation or installation.
    Launch @('/quiet', '/allusers', '/plan', "`"$(Join-Path $work 'opposite.json')`"") 1638
    Launch @('/quiet', '/uninstall')
    CheckRemoved
    $fixture = Join-Path $work 'legacy-user-test.msi'
    Copy-Item -LiteralPath $UserMsi -Destination $fixture -Force
    $db = $engine.OpenDatabase($fixture, 1)
    $view = $db.OpenView("UPDATE ``Property`` SET ``Value`` = '$legacyCode' WHERE ``Property`` = 'ProductCode'")
    [void]$view.Execute(); [void]$view.Close()
    [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($view)
    $summary = $db.SummaryInformation(1)
    $summary.Property(9) = '{' + [guid]::NewGuid().ToString().ToUpperInvariant() + '}'
    $summary.Persist(); [void]$db.Commit()
    [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($summary)
    [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($db)
    $log = Join-Path $work 'legacy-install.log'
    $process = Start-Process msiexec.exe -ArgumentList @('/i', "`"$fixture`"", '/qn', '/norestart', '/l*v', "`"$log`"") -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "Legacy fixture install failed: $($process.ExitCode)" }
    CheckInstalled
    Launch @('/quiet')
    CheckInstalled
    if ([ZgUnifiedMsiState]::Query($legacyCode) -gt 0) { throw 'Legacy user product was not upgraded.' }
    Launch @('/quiet', '/uninstall')
    CheckRemoved
    Write-Host 'PASS: unified setup default scope, elevation routing, non-elevated user install/repair/remove, legacy MSI upgrade and PATH restoration'
} finally {
    foreach ($code in @($product, $legacyCode)) {
        if ([ZgUnifiedMsiState]::Query($code) -gt 0) {
            $cleanup = Start-Process msiexec.exe -ArgumentList @('/x', $code, '/qn', '/norestart') -WindowStyle Hidden -Wait -PassThru
            if ($cleanup.ExitCode -notin 0,3010) { Write-Warning "Cleanup failed: $($cleanup.ExitCode)" }
        }
    }
}
