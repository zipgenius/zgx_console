$ErrorActionPreference = 'Stop'
$engine = Join-Path $PSScriptRoot 'third_party/7z.exe'
$scratch = Join-Path ([IO.Path]::GetTempPath()) ('ZipGeniusSfx-password-test-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($scratch)
$checks = 0
function Assert-Check($condition, $message) {
    if (!$condition) { throw $message }
    $script:checks++
}
try {
    $inputPath = Join-Path $scratch 'content.txt'
    [IO.File]::WriteAllText($inputPath, 'SFX encrypted archive regression')
    foreach ($kind in @('ZipCrypto', 'AES256', '7zHeaders')) {
        $archive = Join-Path $scratch ($kind + $(if ($kind -eq '7zHeaders') { '.7z' } else { '.zip' }))
        $options = if ($kind -eq '7zHeaders') { @('-t7z', '-mhe=on') } else { @('-tzip', "-mem=$kind") }
        & $engine a @options '-ptest-password' $archive $inputPath | Out-Null
        Assert-Check ($LASTEXITCODE -eq 0) "${kind}: creation failed"
        $listing = & $engine l -slt '-p' $archive 2>&1
        $listCode = $LASTEXITCODE
        if ($kind -eq '7zHeaders') {
            Assert-Check ($listCode -ne 0) 'Encrypted headers should require a password to list'
        } else {
            Assert-Check ($listCode -eq 0) "$kind listing should succeed without a password"
            Assert-Check ($listing -contains 'Encrypted = +') "$kind encryption was not detected"
        }
        $wrong = & $engine t '-pwrong-password' $archive 2>&1
        Assert-Check ($LASTEXITCODE -ne 0) "$kind accepted a wrong password"
        Assert-Check (($wrong -join "`n") -match 'Wrong password|password is incorrect') "$kind missing password error classification"
        & $engine t '-ptest-password' $archive | Out-Null
        Assert-Check ($LASTEXITCODE -eq 0) "$kind rejected the correct password"
        $destination = Join-Path $scratch $kind
        & $engine x -y '-ptest-password' "-o$destination" $archive | Out-Null
        Assert-Check ($LASTEXITCODE -eq 0) "$kind extraction failed"
        Assert-Check ((Get-FileHash (Join-Path $destination 'content.txt')).Hash -eq (Get-FileHash $inputPath).Hash) "$kind contents differ"
    }
    # A single ZIP containing an unencrypted file and three password groups.
    $mixed = Join-Path $scratch 'mixed.zip'
    $names = @('plain.txt', 'first.txt', 'folder/second [1].txt', 'third.txt')
    $passwords = @('', 'first-password', 'second-password', 'third-password')
    for ($i = 0; $i -lt $names.Count; $i++) {
        $file = Join-Path $scratch $names[$i]
        [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($file))
        [IO.File]::WriteAllText($file, ('contents ' + $names[$i]))
        $options = if ($i -eq 0) { @() } else { @('-mem=AES256', ('-p' + $passwords[$i])) }
        Push-Location $scratch
        try { & $engine a -tzip @options $mixed $names[$i] | Out-Null } finally { Pop-Location }
        Assert-Check ($LASTEXITCODE -eq 0) 'Mixed ZIP creation failed'
    }
    $destination = Join-Path $scratch 'mixed-output'
    $selection = @()
    $previous = @{}
    foreach ($pass in @('first-password', 'wrong-password', 'second-password', 'third-password')) {
        $output = & $engine x -y -spd '-sccUTF-8' ('-p' + $pass) "-o$destination" @selection $mixed 2>&1
        $code = $LASTEXITCODE
        foreach ($name in $previous.Keys) {
            Assert-Check ((Get-Item -LiteralPath (Join-Path $destination $name)).LastWriteTimeUtc -eq $previous[$name]) 'A successful entry was overwritten'
        }
        $failed = @($output | ForEach-Object { if ("$_" -match 'ERROR: .*Wrong password.*? : (.*)$') { $Matches[1] } })
        if ($pass -eq 'third-password') {
            Assert-Check ($code -eq 0) 'Final password group did not finish'
            Assert-Check ($failed.Count -eq 0) 'Unexpected final password failures'
        } else {
            Assert-Check ($code -ne 0 -and $failed.Count -gt 0) 'Expected retryable password failures'
            if ($pass -eq 'first-password') { Assert-Check ($failed.Count -eq 2) 'Incorrect first-pass pending files' }
            if ($pass -eq 'wrong-password') { Assert-Check ($failed.Count -eq 2) 'Wrong password lost pending files' }
            if ($pass -eq 'second-password') { Assert-Check ($failed.Count -eq 1) 'Incorrect second-group pending files' }
            $list = Join-Path $scratch 'retry.txt'
            [IO.File]::WriteAllLines($list, [string[]]$failed, [Text.UTF8Encoding]::new($false))
            $selection = @('-scsUTF-8', "-i@$list")
        }
        foreach ($name in $names) {
            if ($failed -notcontains $name -and (Test-Path -LiteralPath (Join-Path $destination $name))) {
                $previous[$name] = (Get-Item -LiteralPath (Join-Path $destination $name)).LastWriteTimeUtc
            }
        }
    }
    foreach ($name in $names) {
        Assert-Check ((Get-FileHash -LiteralPath (Join-Path $scratch $name)).Hash -eq (Get-FileHash -LiteralPath (Join-Path $destination $name)).Hash) 'Mixed ZIP content mismatch'
    }
    "$checks encrypted archive checks passed"
} finally {
    # Only remove this test's explicitly rooted, generated temporary directory.
    $resolved = [IO.Path]::GetFullPath($scratch)
    if ([IO.Path]::GetDirectoryName($resolved) -eq [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')) {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
