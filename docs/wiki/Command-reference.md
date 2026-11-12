# ZipGenius X console

Version 1.0 - 12 November 2026

[Italiano](Guida-completa) | [English](Command-reference) | [PDF](https://github.com/zipgenius/zgx_console/wiki/downloads/ZipGenius-X-console-en.pdf)

<a name="usage"></a>

## General usage

`zg -command filename {command parameters} [global options]`

In PowerShell, use the `&` invocation operator when the executable path contains spaces:

```
& "C:\path\zg.exe" -list "C:\Archives\documents.zip"
```

`-add` inclusion parameters must keep the `+` sign in the same string as the path, for example `"+C:\Pictures\*.*"`.

<a name="summary"></a>

## Command syntax table

| Command | Short syntax | Purpose |
| --- | --- | --- |
| [`-add`](#add) | `zg -add archive +[file] [parameters]` | Create or update an archive. |
| [`-list`](#list) | `zg -list archive` | List archive contents. |
| [`-extract`](#extract) | `zg -extract archive destination` | Extract an archive. |
| [`-hash`](#hash) | `zg -hash file --algorithm` | Calculate hashes. |
| [`-delete`](#delete) | `zg -delete archive.zip +[file]` | Delete ZIP entries. |
| [`-destroy`](#destroy) | `zg -destroy file [passes]` | Overwrite the contents of a file. |
| [`-dumpdisc`](#dumpdisc) | `zg -dumpdisc image [options]` | Inspect ISO/VHD/VHDX images. |
| [`-hexview`](#hexview) | `zg -hexview file [parameters]` | Display a hexadecimal view. |
| [`-install`](#install) | `zg -install [--system-wide]` | Install the application. |
| [`-uninstall`](#install) | `zg -uninstall [--system-wide]` | Uninstall the application. |
| [`-help`](#help) | `zg -help [-command]` | Show built-in help. |
| [`-test`](#test) | `zg -test archive [Ppassword]` | Check integrity without extracting files. |
| [`-backup`](#backup) | `zg -backup archive source [options]` | Create a ZIP backup. |
| [`-convert`](#convert) | `zg -convert source destination [options]` | Convert an archive to another format. |
| [`-sfx`](#sfx) | `zg -sfx archive output [options]` | Create a self-extracting package with a window or terminal interface. |
| [`-info`](#info) | `zg -info file [--verbose]` | Show file, archive and disk image information. |

<a name="options"></a>

## Global options

| Option | Effect |
| --- | --- |
| `--silent` | Hide ordinary messages, header and progress. Security warnings remain visible. |
| `--verbose` | Show additional information. |
| `--yes` | Automatically confirm where supported. |
| `--noprogress` | Hide progress bars. |
| `--wait` | Wait for a key before exiting. |

<a name="zip-security"></a>

## Protection from dangerous archives

A ZIP can be small but need an enormous amount of space when unpacked. This kind of archive is called a "ZipBomb" and can fill the disk or make the computer stop responding.

zg checks ZIPs automatically. Protection is always on: there is nothing to enable, and it cannot be turned off. --silent and --yes also leave security checks and warnings active.

A warning means the archive looks suspicious: the operation may continue. High risk stops the operation. If a check cannot be completed, zg stops as a precaution; this does not necessarily mean the file is dangerous.

If an operation is blocked, avoid repeatedly trying the same file: ask the sender for a new copy or use a trusted copy. Some password-protected ZIPs containing other ZIPs cannot be fully checked.

The check covers ZIPs and files based on the ZIP format, including renamed ZIPs. It is not a virus scan, and a low score does not guarantee that every file inside is harmless.

### Codes for scripts and automatic tasks

With -list, the final code lets a script decide what to do without reading the message:

0: no significant risk found (score below 40), if the list was read successfully.

41: warning, score from 40 to 59.

42: high risk, score from 60 to 99.

43: incomplete check, unless high risk has already been found.

Normal errors, such as a missing file, keep their own codes.

With -add, -extract, -backup, -convert, -sfx and -test, 42 and 43 mean a security block. A warning allows the task to continue: if it succeeds, the final code is 0.

The list and warnings use separate outputs (stdout and stderr). Saving the list to a file does not hide warnings. In messages, risk-code describes the risk; it is not always the task's final exit code.

<a name="add"></a>

## `-add` alias -a

`zg -add archive {C...} {A...} {F...} {R...} {K...} +[file] -[file]`

Creates or updates ZIP, 7Z, TAR and compressed TAR streams, CAB and SQX archives. Supported compressed TAR forms include `.tar.gz`/`.tgz`, `.tar.bz2`/`.tbz2` and `.tar.Z`/`.taz`. Plain `.bz2` and `.Z` streams contain one file.

| Parameter | Meaning |
| --- | --- |
| `+[file]` | Include a file, folder or wildcard. Repeatable. |
| `-[file]` | Exclude a file, folder or wildcard. Repeatable. |
| `@list.txt` | Read inclusions and exclusions from a text file. Every line must use `+[file]` to include or `-[file]` to exclude a path or wildcard. |
| `C0 C1 C5 C9 CB` | No, fast, normal, maximum or brutal compression. |
| `AA AM AU` | Add, move and delete sources, or update/replace. |
| `F0 F1 F2 F3` | No path, relative, full without drive, full with drive. |
| `R0 R1` | Disable or enable recursive scanning. |
| `K0 K1` | Hide or show progress. |
| `Ppassword` | Set a password for compatible formats. |
| `Q1 Q2 Q3` | ZIP Crypto, AES-128 or AES-256. |
| `Vvalue` | Volume size for compatible formats. |

### Examples

```
zg -add "C:\Archives\pictures.zip" C9 AU R1 F1 K1 "+C:\Pictures\*.*"
zg -add "C:\Archives\pictures.tar.gz" C5 AU R1 F1 K1 "+C:\Pictures\*.*"
zg -add "C:\Archives\pictures.tar.bz2" C5 AU R1 F1 K1 "+C:\Pictures\*.*"
zg -add "C:\Archives\documents.7z" C9 AA R1 F1 K1 "+C:\Documents\*.pdf" "-draft*"
zg -add "C:\Archives\single.bz2" C5 AU F0 K1 "+C:\Data\report.csv"
```

### ZIP protection

Before creating or updating the archive, zg checks the existing ZIP and the ZIPs selected for addition, including those in included subfolders. A warning allows it to continue; high risk or an incomplete check stops it before changing the archive or moving the original files.

[Warnings, blocks and codes for scripts](#zip-security)

To protect a ZIP with a password, use `-add` with `Ppassword` and `Q3` (AES-256). Example: `zg -add documents.zip Psecret Q3 +document.txt`. Replace `secret` with your own password.

<a name="list"></a>

## `-list` alias -l

`zg -list archive [--verbose]`

Lists archive files. `--verbose` adds sizes, dates, compression ratio and CRC when available.

### Examples

```
zg -list "C:\Archives\pictures.zip"
zg -list "C:\Archives\backup.7z" --verbose
```

### ZIP protection

Shows file names without extracting them to disk and reports anything suspicious. You can therefore view the list even when extraction would be blocked, if the list is readable. The score is an estimated risk level, not a certainty or an exact probability.

[Warnings, blocks and codes for scripts](#zip-security)

<a name="extract"></a>

## `-extract` alias -x

`zg -extract archive destination [options]`

This command extracts one or more files from the specified archive. `archive` identifies the file to decompress; `destination` identifies the folder in which extracted files will be saved. Both parameters are required. Use `./` or `-curr` to extract into the current folder. When no `+[file]` parameter is supplied, all files allowed by the other options are considered.

### Options

|  |  |
| --- | --- |
| `R0 R1` | Extract only root files or include files in archive subfolders. Default: R1. |
| `F0 F1` | Ignore stored paths or rebuild the original folder structure in the destination. Default: F1. |
| `N0 N1` | Extract into the destination or create an archive-named subfolder inside it. Default: N0. |
| `O0 O1 O2` | When files already exist: overwrite them, skip them, or ask the user. Default: O2. |
| `Ppassword` | Specifies the password required to decompress a protected archive. If it is missing or incorrect, interactive sessions prompt up to three times; `Esc` cancels. No prompt is displayed with `--silent`. |
| `K0 K1` | Hide or show the progress indicator. Default: K1. |
| `+[file]` | Include the specified file, folder, or wildcard in extraction. Wildcards `*` and `?` are supported; the parameter is repeatable. |
| `-[file]` | Exclude the specified file, folder, or wildcard from extraction. Wildcards `*` and `?` are supported; the parameter is repeatable. |
| `@list.txt` | Read inclusions and exclusions from a text file whose lines begin with `+[file]` or `-[file]`. |
| `Dattributes` | Extract only files with the specified attributes: A archive, H hidden, R read-only, S system, Z none. X accepts any attribute. |

### Special destinations

Use one of these parameters in place of the destination path.

| Parameter | Destination |
| --- | --- |
| `[DOCS]` | Current user's Documents folder. |
| `[MUSIC]` | Current user's Music folder. |
| `[PICS]` | Current user's Pictures folder. |
| `[VIDEO]` | Current user's Videos folder. |
| `[FAV]` | Current user's Favorites folder. |
| `[SYS]` | Windows system folder. |
| `[WIN]` | Windows installation folder. |
| `[TEMP]` | Temporary files folder. |
| `[CURR]` | Current working directory. |
| `[DESKTOP]` | Current user's Desktop folder. |

### Examples

```
zg -extract "C:\Archives\pictures.zip" "C:\Extracted\Pictures" R1 F1 O0
zg -extract "C:\Archives\protected.7z" "D:\Restore" R1 F1 O0 --noprogress
zg -extract "C:\Archives\backup.7z" -curr R1 Psecret +"*.txt" --silent
```

### ZIP protection

Before extracting, zg checks the ZIP. A warning allows extraction to continue; high risk or an incomplete check stops it before the destination folder is created. The whole ZIP is checked, even when you select only a few files to extract.

[Warnings, blocks and codes for scripts](#zip-security)

<a name="hash"></a>

## `-hash` alias -hh

`zg -hash file --algorithm [--another-algorithm ...]`

During calculation, the current algorithm and an activity indicator are shown. With multiple algorithms, a blank line follows each hash.

### Parameters

| Parameter | Meaning |
| --- | --- |
| `file` | Path to the file to hash. Required. |
| `--md5` | Calculate MD5. |
| `--sha1` | Calculate SHA-1. |
| `--sha224`, `--sha256`, `--sha384`, `--sha512` | Calculate the specified SHA-2 variant. |
| `--sha3-224`, `--sha3-256`, `--sha3-512` | Calculate the specified SHA-3 variant. |
| `--ripemd128`, `--ripemd160`, `--ripemd256`, `--ripemd320` | Calculate the specified RIPEMD variant. |
| `--tiger` | Calculate Tiger. |
| `--whirlpool` | Calculate Whirlpool. |
| `--all` | Calculate all available algorithms. Overrides individual algorithm selections. |
| `--silent` | Show only hash values, without the indicator or labels. Blank lines remain with multiple algorithms. |
| `--noprogress` | Hide the activity indicator during calculation. |

Individual algorithm options can be combined in one command. The other [global options](#options) also apply.

### Examples

```
zg -hash "C:\Downloads\setup.exe" --sha256
zg -hash "C:\Downloads\setup.exe" --md5 --sha1 --sha3-256
zg -hash "C:\Downloads\setup.exe" --all
```

<a name="delete"></a>

## `-delete` alias -d

`zg -delete archive.zip +[file] [...]`

Deletes one or more entries from a ZIP archive. Use `+[file]` to select entries and `-[file]` to exclude matching entries; wildcards are supported.

### Examples

```
zg -delete "C:\Archives\data.zip" "+temp\*"
zg -delete "C:\Archives\data.zip" "+readme.old" "+cache\*" K1
```

<a name="destroy"></a>

## `-destroy`

`zg -destroy file [number-of-passes] [--yes]`

Overwrites the file with random bytes. The file remains on disk. Up to 10 passes are allowed. This operation cannot be undone.

### Examples

```
zg -destroy "C:\Archives\private.zip"
zg -destroy "C:\Archives\private.zip" 3 --yes
```

<a name="dumpdisc"></a>

## `-dumpdisc` alias -dd

`zg -dumpdisc image.iso [--only-dirs|--only-files] [--show-size] [--show-summary] [--show-title] [--show-hash]`

Inspects ISO, VHD and VHDX images. Detailed switches are especially useful for ISO images.

### Parameters

| Parameter | Meaning |
| --- | --- |
| `image.iso` | Path to the ISO, VHD or VHDX image to inspect. Required. |
| `--only-dirs` (`-od`) | Show only directories in an ISO image. |
| `--only-files` (`-of`) | Show only files in an ISO image. When combined with `--only-dirs`, both are shown. |
| `--show-size` (`-sz`) | Show file sizes in the ISO listing. |
| `--show-summary` (`-sm`) | Show total directory and file counts for an ISO image. |
| `--show-title` (`-st`) | Show an ISO header with image name, size and volume metadata. |
| `--show-hash` (`-sh`) | Calculate and show the ISO image's SHA-256 in the header; requires `--show-title`. |
| `--show-date` (`-sd`) | Currently shows file sizes, like `--show-size`. |

The listed options apply to ISO images. For VHD and VHDX, the command shows partition contents.

### Examples

```
zg -dumpdisc "C:\Images\system.iso" --show-title --show-size --show-summary
zg -dumpdisc "C:\Images\system.iso" --only-files --show-hash
```

<a name="hexview"></a>

## `-hexview`

`zg -hexview file [P] [M] [D] [S=offset] [L=lines] [FB=hex-bytes|FT=text] [BL=lines]`

### Options

| Parameter | Meaning |
| --- | --- |
| `file` | Path to the file to display. Required. |
| `P` | Pause every 20 lines, unless `L=` sets another interval. |
| `M` | Disable alternating line colors. |
| `D` | Show decimal rather than hexadecimal offsets. |
| `S=offset` | Start displaying at the specified byte offset. Default: 0. |
| `L=lines` | Pause after the specified number of lines (1 to 500). Default: 20. |
| `FB=hex-bytes` | Search the file for a hexadecimal byte sequence, for example `FB=504B0304`. |
| `FT=text` | Search for UTF-8 text; matching is case sensitive. |
| `BL=lines` | Number of lines shown for each `FB=` or `FT=` result (1 to 10). Default: 1. |

Write the parameters in uppercase, as shown in the examples.

### Examples

```
zg -hexview "C:\Data\file.bin"
zg -hexview "C:\Data\file.bin" M D S=1024 L=40
```

<a name="install"></a>

## `-install` and `-uninstall`

`zg -install [--system-wide|-s]`  
`zg -uninstall [--system-wide|-s]`

Adds or removes the application directory from PATH for the current user. Use `--system-wide` or `-s` to apply the change to all users; this requires administrator privileges.

### Examples

```
zg -install
zg -install --system-wide
zg -uninstall -s
```

<a name="help"></a>

## `-help` alias -?

`zg -help [-command]`

Shows general help or the built-in page for a specific command.

### Examples

```
zg -help
zg -help -add
zg -help -hash
zg -help -test
zg -help -exitcodes
```

<a name="backup"></a>

## `-backup`

`zg -backup archive source [options]`

Backup uses the same checks as -add: it checks the existing backup ZIP and the ZIPs selected for saving. A warning allows it to continue; high risk or an incomplete check stops the backup before changes are made.

`zg -help -backup`: shows all command options.

[Protection from dangerous archives](#zip-security)

<a name="convert"></a>

## `-convert`

`zg -convert source destination [options]`

Conversion first needs to extract the contents. If the source ZIP is high risk or cannot be fully checked, zg stops without replacing the destination file. A warning allows it to continue. Any extracted ZIPs added to the new archive are checked too. The source archive stays unchanged.

`zg -help -convert`: shows all command options.

[Protection from dangerous archives](#zip-security)

## Unrecognised commands

The -split command is not available in zgx. If you use it, zg reports an unrecognised command and exits with code 99.

If a command name is not recognised, zg displays a message and exits with code 99. Use zg -help to see the available commands. Global options with two dashes, such as --p to hide progress, remain available.

<a name="sfx"></a>

## `-sfx`

`Syntax:`

`zg -sfx archive.zip output [--ui=gui|terminal] [--target=platform] [options]`

Creates a self-extracting package that unpacks files without installing ZipGenius X, .NET or 7-Zip on the recipient’s computer. Supports ZIP, 7Z and RAR sources. Choose the operating system and processor type of the computer that will open it.

The original archive stays unchanged. This command creates the package without launching it or running any files inside.

### Options

--ui=gui Window with buttons, folder selection, password and progress (default). Requires a desktop session.

--ui=terminal Questions and progress in a terminal window. Also works on Linux without a desktop.

--target=platform win-x64, win-arm64, linux-x64, linux-arm64, osx-x64 or osx-arm64. Defaults to the operating system and processor of the computer creating the package.

--message="text" Message shown to the recipient (1 to 319 characters).

--auto-start Starts extraction when the recipient opens the package. Normally waits for the Extract button.

--file-list=hidden Hides the file list (default).

--file-list=expanded Shows the file list.

--file-list=collapsed Starts with the file list collapsed; the recipient can expand it.

--no-readme Does not automatically open a Readme after extraction. Normally opens it if present.

--keep-open Keeps the window open after extraction. Normally closes after successful extraction.

--destination=folder Extraction folder: editable, desktop, documents, downloads, pictures, music, videos or favorites.

With editable (default), the recipient chooses the path. Other choices use folders on the recipient's computer and cannot be changed in the SFX window.

--subfolder="path" Subfolder within the selected destination, such as Project/Documents. Requires a destination other than editable. Use a relative path without .., up to 127 characters.

--run-after="file" Opens an archived file after extraction, such as folder/instructions.txt. Use its path inside the archive, not a command with arguments. No extra file is opened by default.

--icon="file.ico" Uses a custom icon. Otherwise uses the standard icon.

--overwrite Replaces an existing .exe only after the new package has been created successfully. Without it, existing files are not replaced.

An archive password is not stored in the SFX: the recipient is prompted when needed to extract. For --run-after, the archive's file list must be readable without a password.

### ZIP protection

The source ZIP is checked before packaging. A warning allows creation to continue; high risk (code 42) or an incomplete check (code 43) stops creation. Protection cannot be disabled, and warnings remain visible with --silent and --yes. This ZipBomb check covers ZIPs, not the other formats.

### Examples

```
zg -sfx documents.zip documents.exe
```

```
zg -sfx documents.7z documents.exe --message="Project documents" --file-list=expanded
```

```
zg -sfx pictures.zip pictures.exe --destination=pictures --subfolder=Holiday --overwrite
```

```
zg -sfx project.zip project.exe --run-after=instructions.txt --icon=project.ico
```

Use zg -help -exitcodes to learn about final exit codes.

Everything needed to create an SFX is built into zgx. The command also works in the standalone console version: there is no need to install the main app or obtain sfx\_stub.exe. The temporary copy used during creation is removed afterwards.

### Platforms and output names

Windows produces an .exe. Linux produces an executable such as documents.run; if needed, run chmod +x documents.run before opening it. On macOS the GUI package is an .app, while the terminal package is an executable.

Creating and locally signing a macOS package requires a Mac. This local signature does not replace signing and notarization for public distribution on macOS.

Each zgx distribution embeds the templates prepared during its build, without external stub files. If the requested template is missing, the command reports it instead of producing a package for another system.

Custom icons require --ui=gui. Customizing the Windows x64 GUI icon requires creating that package on Windows.

### Using a terminal package

Launch it and follow the questions. It shows the proposed folder, asks for confirmation and requests a password only when needed. Ctrl+C cancels; typed passwords are hidden.

For automation use: ./documents.run --yes --destination=/path/to/extracted --no-open

--overwrite allows replacing existing extracted files. --list shows file names. --no-open prevents opening Readme or other files afterwards. --password=value supplies a password without prompts; for interactive use, prefer typing it when prompted.

A destination fixed by the author cannot be changed with --destination.

### Examples for other platforms

```
zg -sfx documents.zip documents.exe --ui=terminal --target=win-x64
```

```
zg -sfx documents.zip documents.run --ui=terminal --target=linux-x64
```

```
zg -sfx documents.zip documents.run --ui=gui --target=linux-x64
```

```
zg -sfx documents.zip Documents.app --ui=gui --target=osx-arm64
```

To see the templates included in your copy of zgx, use: zg -sfx --list-targets

<a name="test"></a>

## `-test` alias -t

`Syntax:`

`zg -test archive [Ppassword] [--verbose] [--silent]`

`Alias: zg -t archive`

Checks whether an archive's files can be read and whether the available integrity checks pass. This is useful before keeping a backup or after downloading an archive.

The command reads data without saving extracted files. It does not change the archive or ask for a destination folder. A valid empty archive also passes.

### Options

Ppassword Archive password. You can also use --password=value.

--verbose Shows checked entries and available details.

--silent Hides the header, progress and success message. Errors and security warnings remain visible.

--noprogress Hides the progress indicator.

When a password is needed in an interactive session, it is requested without showing typed characters. You can retry up to three times; Esc cancels. With --silent or redirected input, no questions are asked: provide the password in the command.

A wrong password and damaged encrypted data cannot always be distinguished. Neither is reported as a successful test.

### Protection from dangerous archives

ZipBomb protection is always active for -test too: checking integrity requires decompressing data and can consume many resources. A warning allows the test to continue. High risk or an incomplete security check stops the test before decompression. --yes and --silent cannot override a block.

### Final codes for scripts

0 Test completed; no errors found by the available checks.

3 Archive not found.

6 Test failed: damaged or unreadable archive data or structure.

7 The format or test operation is not supported by the available engine.

8 Protected data not verified: missing or wrong password, or damaged encrypted data.

20 Cancelled.

30 Cannot access the archive or testing engine.

42 Blocked because of high ZipBomb risk.

43 Blocked because the security check is incomplete. Damaged ZIP headers can also cause this result.

99 Invalid syntax or option.

A moderate-risk warning does not decide the final result: a successful subsequent test returns 0; a failure returns the relevant error code.

In PowerShell read $LASTEXITCODE immediately afterwards. In a Linux or macOS terminal use $?.

### Formats and limits

ZIPs use managed engines on every platform, including AES-protected ZIPs. On Windows, other formats read by the 7-Zip engine, including 7Z, RAR, CAB and TAR, use its internal test. Linux and macOS use the managed reader for formats it recognises, including 7Z, RAR, TAR and GZIP; some other formats may return 7.

SQX testing is not yet available and returns 7. The command does not extract files as a fallback.

Checks depend on the format. For example, TAR has no check value for individual file data, so not every change to its contents can be detected. This is not a virus scan and does not recursively test archives stored as ordinary files inside another archive.

### Examples

```
zg -test documents.zip
```

```
zg -t backup.7z --verbose
```

```
zg -test protected.zip Psecret --silent
```

```
zg -test protected.zip --password=secret --noprogress
```

### Automatic check in PowerShell

```
zg -test "backup.zip" --silent
$testResult = $LASTEXITCODE
if ($testResult -eq 0) { "Archive verified" } else { "Check did not succeed: code $testResult" }
```

### Linux or macOS terminal example

```
zg -test backup.zip --silent
test_result=$?
if [ "$test_result" -eq 0 ]; then echo "Archive verified"; else echo "Check did not succeed: code $test_result"; fi
```

A nonzero code needs attention, but does not always mean damaged files. A password may be missing, the format may be unsupported, or security protection may have stopped the check.

<a name="info"></a>

## `-info`

File information: -info  
Syntax: zg -info file [--verbose] [--silent]

Shows a table of information read from the file. It opens the file read-only: it does not mount images, extract files or change the original.

The standard report shows the full path, exact file length in bytes, last change in UTC and the format reader. Identification uses file contents where possible; the extension may select a reader, but does not prove that the file is valid.

For ISO and UDF images it shows the filesystem, volume label, sector size, sector count and, for ISO 9660, the primary volume information and whether a boot image is present.  
For VHDX, VHD, VMDK, VDI, DMG and RAW/IMG/IMA images it shows virtual capacity, disk type, partitions, volumes and recognised filesystems. The file length and virtual capacity are different values: a dynamic disk can have a much smaller file than its capacity. Geometry is a compatibility value, not a second measurement of capacity.  
For archives supported by the available reader it shows the number of files and folders, declared sizes and encryption information. Windows can also use the bundled 7-Zip reader for other archive formats.

--verbose adds all scalar metadata exposed by these readers: disk layers, sparse storage, parent requirements, identifiers, geometry, filesystem details and format-specific information. For VHDX this includes block size, logical/physical sectors, creator and header identifiers. For VMDK it includes creation type, adapter and extent paths when available. For ISO it adds publisher, preparer, application, volume set, timestamps and boot details. For archives it adds an entry table with sizes, encryption, checksum and modification time where available.  
Not every format or reader provides every field. Missing or unsupported values are marked as not available. A reported checksum is stored metadata, not a new verification: use -test to check archive integrity. The command does not scan every file inside a disk or compute a full-file hash. It is not a virus scan.

For split or differencing disks keep the extent files and parent images in their expected locations. Missing parts, unsupported features or unreadable metadata may prevent a complete report. Unknown filesystems can be blank, encrypted or unsupported; this is not proof of damage. macOS and Linux use the managed readers; reader support can differ from Windows.

The report uses stable English field names, exact byte counts and invariant numbers on every system. Tabs and control characters in file names are replaced with spaces. --silent hides the banner but keeps the report; --verbose still applies with --silent. Diagnostics go to stderr and tables go to stdout, so the report can be redirected to a text file.

ZIP security checks stay active. Like -list, -info may show metadata for a suspicious archive and returns the associated risk code. It does not extract its contents.

Exit codes:  
0: report completed with the available metadata.  
3: file or required component not found.  
7: unsupported format or operation.  
9: incomplete report or unreadable/damaged metadata. Any tables already printed remain useful, but are not a complete result.  
30: access denied.  
41: ZIP risk warning.  
42: high ZIP risk.  
43: ZIP safety check incomplete.  
99: invalid arguments.  
A metadata reading error takes precedence over ZIP risk codes; security warnings remain on stderr. A completed ZIP report returns its risk code when nonzero.

Examples:  
zg -info installation.iso  
zg -info machine.vhdx --verbose  
zg -info machine.vmdk --verbose --silent > report.txt  
zg -info backup.zip --verbose  
Help: zg -help -info

Encrypted archive headers may prevent reading the report: this command does not request a password. SQX metadata is not exposed by its engine and returns 7. Extra VDI identifiers are read for VDI 1.1; other versions show the metadata available from the reader.