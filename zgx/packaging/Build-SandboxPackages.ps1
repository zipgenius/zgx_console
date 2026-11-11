param(
    [string]$Distribution = 'Ubuntu-26.04',
    [ValidateSet('snap', 'flatpak', 'all')][string]$Format = 'all',
    [string]$Runtime = '25.08'
)
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
$scriptPath = LinuxPath (Join-Path $PSScriptRoot 'build-sandbox.py')
$sourcePath = LinuxPath $payload
$distPath = LinuxPath (Join-Path $PSScriptRoot 'dist')
& wsl -d $Distribution --exec python3 $scriptPath --payload $sourcePath --output $distPath --version $version --format $Format --runtime $Runtime
if ($LASTEXITCODE -ne 0) { throw "Sandbox packaging failed: $LASTEXITCODE" }
Write-Host "Snap/Flatpak version: $version; packages: $PSScriptRoot/dist"
