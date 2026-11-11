param([string]$Msi, [switch]$UserScope)
$ErrorActionPreference = 'Stop'
if (!$Msi) {
    $filter = if ($UserScope) { 'zg-console-*-win-x64-user.msi' } else { 'zg-console-*-win-x64.msi' }
    $latest = Get-ChildItem "$PSScriptRoot/dist" -Filter $filter | Sort-Object { [version]($_.Name -replace '^zg-console-|-win-x64(-user)?\.msi$', '') } -Descending | Select-Object -First 1
    if (!$latest) { throw 'No MSI package found' }
    $Msi = $latest.FullName
}
$Msi = (Resolve-Path $Msi).Path
if ($Msi -like '*-win-x64-user.msi') { $UserScope = $true }
$installer = New-Object -ComObject WindowsInstaller.Installer
$db = $installer.OpenDatabase($Msi, 0)
function Query([string]$Sql, [int]$Columns) {
    $view = $db.OpenView($Sql)
    [void]$view.Execute()
    while ($record = $view.Fetch()) {
        $values = @(for ($i = 1; $i -le $Columns; $i++) { $record.StringData($i) })
        ,$values
    }
    [void]$view.Close()
}
$actions = @{}
foreach ($row in Query 'SELECT `Action`, `Type`, `Source`, `Target` FROM `CustomAction`' 4) {
    $actions[$row[0]] = $row
}
foreach ($name in 'RegisterZg', 'UnregisterZg', 'RollbackRegister', 'RollbackUnregister') {
    $row = $actions[$name]
    if (!$row) { throw "Missing custom action $name" }
    $type = [int]$row[1]
    if (((($type -band 0x800) -ne 0) -eq [bool]$UserScope) -or ($type -band 0x400) -eq 0 -or ($type -band 0x40) -ne 0) {
        throw "Incorrect deferred action identity or return policy: $name"
    }
    if ($row[2] -ne 'ZgExecutable') { throw "Wrong executable: $name" }
    $expected = if ($name -in 'RegisterZg', 'RollbackUnregister') { '-install' } else { '-uninstall' }
    $scopeArgument = if ($UserScope) { '' } else { ' --system-wide' }
    if ($row[3] -ne "$expected$scopeArgument --silent") { throw "Wrong arguments: $name" }
    Write-Host "$name : $($row[3])"
}
$sequence = @{}
foreach ($row in Query 'SELECT `Action`, `Condition`, `Sequence` FROM `InstallExecuteSequence`' 3) { $sequence[$row[0]] = $row }
if ([int]$sequence['RegisterZg'][2] -le [int]$sequence['InstallFiles'][2]) { throw 'Registration before copy' }
if ([int]$sequence['UnregisterZg'][2] -ge [int]$sequence['RemoveFiles'][2]) { throw 'Unregistration after removal' }
if ([int]$sequence['RemoveExistingProducts'][2] -ge [int]$sequence['InstallFiles'][2]) { throw 'Unsafe upgrade order' }
if ($sequence['UnregisterZg'][1] -ne 'REMOVE~="ALL"') { throw 'Incorrect removal condition' }
if ($sequence['RegisterZg'][1] -ne 'NOT REMOVE~="ALL"') { throw 'Incorrect installation condition' }
foreach ($row in Query 'SELECT `Value` FROM `Property` WHERE `Property` = ''ARPCOMMENTS''' 1) { $description = $row[0] }
if (!$description -or $description -notmatch 'ZipBomb protection is always active' -or $description -match 'Self-contained .NET') { throw 'MSI description mismatch' }
if ($UserScope) {
    foreach ($row in Query 'SELECT `Value` FROM `Property` WHERE `Property` = ''ALLUSERS''' 1) {
        if ($row[0]) { throw 'Per-user MSI declares ALLUSERS' }
    }
    if (($db.SummaryInformation(0).Property(15) -band 8) -eq 0) { throw 'Per-user MSI requests elevated privileges' }
    foreach ($row in Query 'SELECT `Root` FROM `Registry`' 1) {
        if ($row[0] -ne '1') { throw 'Per-user MSI writes outside HKCU' }
    }
    Write-Host 'PASS: limited privileges, no ALLUSERS, HKCU registry and impersonated actions'
}
$extractionName = if ($UserScope) { 'work/msi-user-extracted' } else { 'work/msi-extracted' }
$extract = Join-Path $PSScriptRoot $extractionName
New-Item -ItemType Directory -Force $extract | Out-Null
$log = Join-Path $PSScriptRoot 'work/msi-extract.log'
$process = Start-Process msiexec.exe -ArgumentList @('/a', "`"$Msi`"", '/qn', "TARGETDIR=`"$extract`"", '/l*v', "`"$log`"") -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0) { throw "MSI extraction failed: $($process.ExitCode); $log" }
$exe = Get-ChildItem $extract -Filter zg.exe -Recurse | Select-Object -First 1
if (!$exe) { throw 'Executable absent from MSI' }
foreach ($row in Query 'SELECT `Value` FROM `Property` WHERE `Property` = ''ProductVersion''' 1) { $packageVersion = $row[0] }
$assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $exe.DirectoryName 'zg.dll')).Version.ToString()
if ([version]$packageVersion -ne [version]$assemblyVersion) { throw "MSI/assembly version mismatch: $packageVersion / $assemblyVersion" }
$banner = & $exe.FullName -yourpath
if ($LASTEXITCODE -ne 0 -or !($banner -match 'https://www.zipgenius.it') -or ($banner -match 'https://www.zipgenius.com')) { throw 'Website URL mismatch' }
Write-Host "Product/assembly version: $assemblyVersion; website: https://www.zipgenius.it"
& $exe.FullName -help -info --silent
if ($LASTEXITCODE -ne 0) { throw 'Extracted application failed' }
& $exe.FullName -sfx --list-targets --silent
if ($LASTEXITCODE -ne 0) { throw 'Extracted SFX templates failed' }
Write-Host 'PASS: MSI action types, arguments, sequence, extraction and standalone execution'
