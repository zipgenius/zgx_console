param([string]$Distribution = 'Ubuntu-26.04', [ValidatePattern('^[1-9]\d*$')][string]$Release = '1')
$ErrorActionPreference = 'Stop'
$payload = Join-Path $PSScriptRoot 'work/linux-x64'
$assembly = Join-Path $payload 'zg.dll'
if (!(Test-Path -LiteralPath $assembly)) { throw 'Generate the Linux publish with Build-Packages.ps1 first.' }
$version = [Reflection.AssemblyName]::GetAssemblyName($assembly).Version.ToString()
function LinuxPath([string]$Path) {
    $result = & wsl -d $Distribution --exec wslpath -a ($Path.Replace('\', '/'))
    if ($LASTEXITCODE -ne 0) { throw "Cannot translate $Path" }
    $result.Trim()
}
$scriptPath = LinuxPath (Join-Path $PSScriptRoot 'build-arch.py')
$sourcePath = LinuxPath $payload
$distPath = LinuxPath (Join-Path $PSScriptRoot 'dist')
& wsl -d $Distribution --exec python3 $scriptPath --payload $sourcePath --output $distPath --version $version --release $Release
if ($LASTEXITCODE -ne 0) { throw "Arch packaging failed: $LASTEXITCODE" }
