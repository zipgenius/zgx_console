# Usage examples

[Home](Home) · [Installation](Installation) · [Italiano](Esempi)

## Command syntax

```text
zg -command archive-or-file command-parameters global-options
```

These examples assume `zg` is available in PATH. Otherwise use the full executable path, `./zg` on Unix, or `.\zg.exe` in PowerShell. Run them in a working folder containing a `documents` subfolder with sample files. Keep output archives outside the input folder. Commands use ordinary straight quotes.

```sh
zg -help
zg -help -add
zg -help -extract
zg -help -test
zg -help -exitcodes
```

## Create and update a ZIP

```sh
zg -add documents.zip "+documents/*" R1 F1 C5
zg -add documents.zip "+notes.txt" C5
```

The first command recursively adds files from `documents`, preserving relative paths with medium compression. The second adds a file to the same archive. Quote the entire `+pattern` so Bash, Zsh and PowerShell pass it as one argument. `R0` excludes subfolders, `F0` omits stored paths, `C0` stores without compression, and `C9` selects maximum compression. The default for `-add` is `C0`; specify the level explicitly when compression matters.

Exclude temporary files:

```sh
zg -add clean.zip "+documents/*" "-*.tmp" R1 F1 C5
```

Use `-help -add` for selection rules and `@list.txt` input. Avoid `AM` unless you intend to move files: it deletes originals after compression.

Create an AES-256 encrypted ZIP:

```sh
zg -add protected.zip "+documents/*" R1 F1 C5 PExampleOnly-ChangeMe Q3
```

Replace the example password. Command-line passwords can appear in shell history and process arguments. Prefer the hidden password prompt for interactive extraction and testing. `Q3` selects AES-256; using only `Ppassword` defaults to legacy ZipCrypto.

## List, test and extract

```sh
zg -list documents.zip --verbose
zg -test documents.zip
zg -extract documents.zip extracted R1 F1 O1
```

`-list` reads metadata; it does not verify file data. `-test` reads archive contents without saving extracted files. `-extract` requires a destination: `O1` skips existing files, `O0` always overwrites, and `O2` asks (default). `F1` rebuilds paths and `R1` includes subfolders. `N1` creates an extra folder named after the archive. Use `-curr` as the destination for the current folder.

Extract only matching text files, excluding drafts:

```sh
zg -extract documents.zip selected R1 F1 O1 "+*.txt" "-draft*"
```

For protected archives, omit the password for an interactive hidden prompt:

```sh
zg -test protected.zip
zg -extract protected.zip extracted-protected O1
```

With `--silent` or redirected input, supply the password explicitly; there is no interactive prompt.

## ZIP backups

```sh
zg -backup backup.zip documents T C5
zg -backup rolling.zip documents C5
zg -backup rolling.zip documents U C5
```

`T` appends `_YYYYMMDD_HHmm` to the filename; subfolders are included by default. Without `U`, an existing backup filename is refused. `U` updates files already present and adds sources; it is not a mirror that promises deletion of obsolete archived entries. Keep backups outside the source tree. `--documents`, `--pictures` and related flags select user folders.

## Hashes and disk information

```sh
zg -hash documents.zip --sha256
zg -hash documents.zip --sha256 --sha512 --silent
zg -info installation.iso --verbose
zg -info machine.vhdx --verbose --silent > disk-report.txt
```

`--silent` prints only hash values for `-hash`; for `-info` it preserves the report. Disk information is read-only and does not mount the image. Keep parent images and split extents in their expected locations. Code `9` means an incomplete information report. Stored checksums shown by metadata commands do not replace `-test`.

## Convert archives

```sh
zg -convert documents.zip documents.tar.gz C5
```

Conversion extracts into a private temporary folder and recreates the destination; allow enough disk space. The source stays unchanged. Existing destinations are refused unless `--overwrite` is supplied. Destination format support depends on the platform: Linux 7Z/TAR.XZ writing requires libarchive; SQX and CAB creation require Windows components. See [platform support](Platform-support).

The Windows build checked for this guide returned code `99` when converting a ZIP containing directory entries to 7Z. Use the TAR.GZ example above for that case; 7Z conversion is not claimed as verified on Windows here.

## Create a self-extracting package

```sh
zg -sfx --list-targets
```

Choose a template actually included in your distribution. For example, on a Windows x64 build:

```powershell
zg -sfx documents.zip documents.exe --ui=terminal --target=win-x64
```

On a Linux x64 build:

```sh
zg -sfx documents.zip documents.run --ui=terminal --target=linux-x64
```

Creation does not launch the result. The recipient does not need ZipGenius or .NET installed. GUI mode requires a desktop session; macOS package creation and local signing require a Mac. Missing cross-platform templates are reported rather than substituted. See `zg -help -sfx` for GUI options and recipient-side automation.

## Scripts and final codes

PowerShell:

```powershell
zg -test documents.zip --silent
$result = $LASTEXITCODE
if ($result -eq 0) { "Archive verified" } else { "Check failed: $result" }
```

Bash/Zsh:

```sh
zg -test documents.zip --silent
result=$?
if [ "$result" -eq 0 ]; then echo "Archive verified"; else echo "Check failed: $result"; fi
```

Read the code immediately. Common `-test` results:

| Code | Meaning |
| --- | --- |
| 0 | Available integrity checks passed |
| 3 | Archive missing |
| 6 | Damaged or unreadable archive |
| 7 | Unsupported format or operation |
| 8 | Protected data not verified |
| 20 | Cancelled |
| 30 | Access/engine error |
| 42 | High ZIP risk; blocked |
| 43 | Incomplete ZIP safety check; blocked |
| 99 | Invalid arguments |

`-list` can return `41` for a warning while still printing the list, or `42`/`43` for risk/incomplete checks. For extraction, addition, backup, conversion and testing, a moderate warning permits the operation and a successful task returns `0`. `--silent` hides routine output, `--noprogress` hides progress, and `--yes` cannot override safety blocks. Check command-specific help before assigning meanings to other exit codes.
