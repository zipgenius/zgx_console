param(
    [ValidatePattern('^\d+\.\d+\.\d+(\.\d+)?$')][string]$Version,
    [string]$Distribution = 'Ubuntu',
    [switch]$SkipPublish,
    [switch]$WindowsOnly,
    [switch]$IncludeSandboxPackages,
    [switch]$IncludeArchPackage,
    [ValidateSet('machine', 'user', 'both', 'unified')][string]$WindowsScope = 'unified'
)
$ErrorActionPreference = 'Stop'
if ($WindowsOnly -and ($IncludeSandboxPackages -or $IncludeArchPackage)) { throw 'Additional Linux packages require the Linux publish; omit -WindowsOnly.' }
$project = Split-Path $PSScriptRoot
$work = Join-Path $PSScriptRoot 'work'
$dist = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force $work, $dist | Out-Null
if (!$Version) {
    if ($SkipPublish) {
        $Version = [Reflection.AssemblyName]::GetAssemblyName("$work/win-x64/zg.dll").Version.ToString()
    } else {
        & dotnet build "$project/zg.csproj" -c Release -v quiet
        if ($LASTEXITCODE) { throw 'Version build failed.' }
        $Version = [Reflection.AssemblyName]::GetAssemblyName("$project/bin/Release/net10.0/zg.dll").Version.ToString()
    }
}
if (($Version.Split('.')).Count -eq 3) { $Version += '.0' }
Write-Host "zg-console version: $Version"
function Run([string]$Command, [string[]]$Arguments) {
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Command failed: $LASTEXITCODE" }
}
$rids = if ($WindowsOnly) { @('win-x64') } else { @('win-x64', 'linux-x64') }
foreach ($rid in $rids) {
    $publish = Join-Path $work $rid
    if (!$SkipPublish) {
        if (Test-Path -LiteralPath $publish) {
            $resolvedPublish = (Resolve-Path -LiteralPath $publish).Path
            $workPrefix = [IO.Path]::GetFullPath($work).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
            if (!$resolvedPublish.StartsWith($workPrefix, [StringComparison]::OrdinalIgnoreCase) -or
                [IO.Path]::GetFileName($resolvedPublish) -ne $rid) {
                throw "Refusing to clean staging directory outside packaging/work: $resolvedPublish"
            }
            Remove-Item -LiteralPath $resolvedPublish -Recurse -Force
        }
        Run 'dotnet' @('publish', "$project/zg.csproj", '-c', 'Release', '-r', $rid,
            '--self-contained', 'true', '-o', $publish, "-p:Version=$Version",
            "-p:AssemblyVersion=$Version", "-p:FileVersion=$Version", "-p:BuildNumber=$($Version.Split('.')[-1])", '-v', 'quiet')
    }
    $exe = if ($rid -eq 'win-x64') { 'zg.exe' } else { 'zg' }
    if (!(Test-Path "$publish/$exe")) { throw "Missing $publish/$exe" }
    $payloadVersion = [Reflection.AssemblyName]::GetAssemblyName("$publish/zg.dll").Version.ToString()
    if ($payloadVersion -ne $Version) { throw "Payload version $payloadVersion differs from package version $Version" }
}
$wix = Join-Path $PSScriptRoot '.tools/wix.exe'
if (!(Test-Path $wix)) {
    Run 'dotnet' @('tool', 'install', '--tool-path', "$PSScriptRoot/.tools", 'wix', '--version', '6.0.2')
}
function EscapeXml([string]$Value) { [System.Security.SecurityElement]::Escape($Value) }
$msiOutput = if ($WindowsScope -eq 'unified') { Join-Path $work 'windows-msi' } else { $dist }
New-Item -ItemType Directory -Path $msiOutput -Force | Out-Null
$scopes = if ($WindowsScope -in 'both', 'unified') { @('machine', 'user') } else { @($WindowsScope) }
foreach ($scope in $scopes) {
$payload = Join-Path $work 'win-x64'
$isUser = $scope -eq 'user'
$packageScope = if ($isUser) { 'perUser' } else { 'perMachine' }
$upgradeCode = if ($isUser) { 'B69EC660-183F-42FA-9D05-0A63C6D61845' } else { '7E0468AB-7C75-4D5E-9726-090E5D431A1A' }
$productName = if ($isUser) { 'ZipGenius X Console (Current user)' } else { 'ZipGenius X Console' }
$rootFolder = if ($isUser) { 'LocalAppDataFolder' } else { 'ProgramFiles64Folder' }
$userProgramsOpen = if ($isUser) { '<Directory Id="UserProgramsFolder" Name="Programs">' } else { '' }
$userProgramsClose = if ($isUser) { '</Directory>' } else { '' }
$systemWide = if ($isUser) { '' } else { ' --system-wide' }
$impersonate = if ($isUser) { 'yes' } else { 'no' }
$suffix = if ($isUser) { '-user' } else { '' }
$xml = [System.Text.StringBuilder]::new()
[void]$xml.AppendLine(@"
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Package Name="$productName" Manufacturer="The ZipGenius Team" Version="$Version"
           UpgradeCode="$upgradeCode" Scope="$packageScope" Language="1033">
    <MajorUpgrade DowngradeErrorMessage="A newer version of ZipGenius X Console is already installed." Schedule="afterInstallInitialize" AllowSameVersionUpgrades="yes" />
    <MediaTemplate EmbedCab="yes" CompressionLevel="high" />
    <Property Id="ARPCOMMENTS" Value="Create and extract archives, check their integrity, view detailed archive and disk image information, and create self-extracting packages. ZipBomb protection is always active." />
    <StandardDirectory Id="$rootFolder">$userProgramsOpen<Directory Id="INSTALLFOLDER" Name="ZipGenius X Console">
"@)
$script:index = 0
$script:components = [System.Collections.Generic.List[string]]::new()
function UserMarker([string]$RelativePath) {
    $bytes = [Text.Encoding]::UTF8.GetBytes($RelativePath.Replace('\', '/').ToLowerInvariant())
    'P' + [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).Substring(0, 32)
}
function AddDirectory([string]$Directory) {
    if ($isUser) {
        $script:index++
        $cleanupId = "C$script:index"
        $script:components.Add($cleanupId)
        $marker = UserMarker ('directory/' + [IO.Path]::GetRelativePath($payload, $Directory))
        [void]$xml.AppendLine("<Component Id=`"$cleanupId`" Guid=`"*`"><RegistryValue Root=`"HKCU`" Key=`"Software\ZipGenius\ZgConsole\UserInstaller`" Name=`"$marker`" Type=`"integer`" Value=`"1`" KeyPath=`"yes`" /><RemoveFolder Id=`"R$script:index`" On=`"uninstall`" /></Component>")
    }
    foreach ($file in Get-ChildItem -LiteralPath $Directory -File | Sort-Object Name) {
        if ($file.Extension -eq '.pdb') { continue }
        $script:index++
        $id = "F$script:index"
        if ($file.FullName -eq (Join-Path $payload 'zg.exe')) { $id = 'ZgExecutable' }
        $component = "C$script:index"
        $script:components.Add($component)
        if ($isUser) {
            $marker = UserMarker ('file/' + [IO.Path]::GetRelativePath($payload, $file.FullName))
            $componentGuid = ([Guid]::new([Convert]::FromHexString($marker.Substring(1)))).ToString()
            [void]$xml.AppendLine("<Component Id=`"$component`" Guid=`"$componentGuid`"><File Id=`"$id`" Source=`"$(EscapeXml $file.FullName)`" KeyPath=`"no`" /><RegistryValue Root=`"HKCU`" Key=`"Software\ZipGenius\ZgConsole\UserInstaller`" Name=`"$marker`" Type=`"integer`" Value=`"1`" KeyPath=`"yes`" /></Component>")
        } else {
        [void]$xml.AppendLine("<Component Id=`"$component`" Guid=`"*`"><File Id=`"$id`" Source=`"$(EscapeXml $file.FullName)`" KeyPath=`"yes`" /></Component>")
        }
    }
    foreach ($dir in Get-ChildItem -LiteralPath $Directory -Directory | Sort-Object Name) {
        $script:index++
        [void]$xml.AppendLine("<Directory Id=`"D$script:index`" Name=`"$(EscapeXml $dir.Name)`">")
        AddDirectory $dir.FullName
        [void]$xml.AppendLine('</Directory>')
    }
}
AddDirectory $payload
[void]$xml.AppendLine("</Directory>$userProgramsClose</StandardDirectory>")
if ($isUser) {
    [void]$xml.AppendLine('<DirectoryRef Id="UserProgramsFolder"><Component Id="UserProgramsCleanup" Guid="*"><RegistryValue Root="HKCU" Key="Software\ZipGenius\ZgConsole\UserInstaller" Name="ProgramsFolder" Type="integer" Value="1" KeyPath="yes" /><RemoveFolder Id="RemoveUserPrograms" On="uninstall" /></Component></DirectoryRef>')
    $script:components.Add('UserProgramsCleanup')
}
[void]$xml.AppendLine('<Feature Id="Main" Title="ZipGenius X Console" Level="1">')
foreach ($id in $script:components) { [void]$xml.AppendLine("<ComponentRef Id=`"$id`" />") }
[void]$xml.AppendLine(@"
    </Feature>
    <CustomAction Id="RegisterZg" FileRef="ZgExecutable" ExeCommand="-install$systemWide --silent" Execute="deferred" Impersonate="$impersonate" Return="check" />
    <CustomAction Id="UnregisterZg" FileRef="ZgExecutable" ExeCommand="-uninstall$systemWide --silent" Execute="deferred" Impersonate="$impersonate" Return="check" />
    <CustomAction Id="RollbackRegister" FileRef="ZgExecutable" ExeCommand="-uninstall$systemWide --silent" Execute="rollback" Impersonate="$impersonate" Return="check" />
    <CustomAction Id="RollbackUnregister" FileRef="ZgExecutable" ExeCommand="-install$systemWide --silent" Execute="rollback" Impersonate="$impersonate" Return="check" />
    <InstallExecuteSequence>
      <Custom Action="RollbackUnregister" Before="UnregisterZg" Condition="REMOVE~=&quot;ALL&quot;" />
      <Custom Action="UnregisterZg" Before="RemoveFiles" Condition="REMOVE~=&quot;ALL&quot;" />
      <Custom Action="RollbackRegister" After="InstallFiles" Condition="NOT REMOVE~=&quot;ALL&quot;" />
      <Custom Action="RegisterZg" After="RollbackRegister" Condition="NOT REMOVE~=&quot;ALL&quot;" />
    </InstallExecuteSequence>
  </Package>
</Wix>
"@)
$wxs = Join-Path $work "Package-$scope.wxs"
[IO.File]::WriteAllText($wxs, $xml.ToString())
Run $wix @('build', $wxs, '-arch', 'x64', '-o', "$msiOutput/zg-console-$Version-win-x64$suffix.msi")
}
if ($WindowsScope -eq 'unified') {
    & "$PSScriptRoot/Build-WindowsSetup.ps1" -MachineMsi "$msiOutput/zg-console-$Version-win-x64.msi" -UserMsi "$msiOutput/zg-console-$Version-win-x64-user.msi"
}
if (!$WindowsOnly) {
$linuxScript = (& wsl -d $Distribution --exec wslpath -a "$PSScriptRoot/build-linux.py").Trim()
$linuxPayload = (& wsl -d $Distribution --exec wslpath -a "$work/linux-x64").Trim()
$linuxDist = (& wsl -d $Distribution --exec wslpath -a $dist).Trim()
Run 'wsl' @('-d', $Distribution, '--exec', 'python3', $linuxScript, '--payload', $linuxPayload, '--output', $linuxDist, '--version', $Version)
}
if ($IncludeSandboxPackages) {
    & "$PSScriptRoot/Build-SandboxPackages.ps1" -Distribution $Distribution
}
if ($IncludeArchPackage) {
    & "$PSScriptRoot/Build-ArchPackage.ps1" -Distribution $Distribution
}
Get-ChildItem $dist -File | Where-Object { $_.Extension -in '.deb', '.rpm', '.msi', '.exe', '.snap', '.flatpak' -or $_.Name -like '*.pkg.tar.zst' } | ForEach-Object {
    $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $($_.Name)"
} | Set-Content "$dist/SHA256SUMS" -Encoding ascii
Write-Host "Packages: $dist"
