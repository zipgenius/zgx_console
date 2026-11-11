# zg-console installers

Build a unified Windows x64 setup EXE and Linux x64 DEB/RPM from the current sources:

```powershell
./Build-Packages.ps1 -Distribution Ubuntu
```

Requirements: Windows, .NET SDK 10, WSL distribution with Python 3,
`dpkg-deb` and `rpmbuild` (`sudo apt-get install rpm`). The script installs
WiX 6.0.2 locally in the ignored `.tools` directory. Outputs and SHA-256
checksums are in `dist/`; intermediate files are in `work/`.
`-SkipPublish` repackages existing `work/win-x64` and `work/linux-x64` payloads.
Normal builds regenerate the SFX templates for each target and include .NET.
Packages are unsigned. The standalone product version is `10.0.1`; each local
build increments its revision in `.build/build-number.txt`. The script allocates
one version by building the console, then reuses it for each published target.
`-Version 10.0.1.N` assigns an explicit shared version; `-SkipPublish` checks the
existing payload version. CI obtains a shared revision from its workflow run.
MSI compares only the first three fields. Same-version upgrades are enabled so
revisions can be replaced; MSI can also accept an older fourth-field revision.
Always select the newest full four-field package version.
Use `-WindowsOnly` to build the unified Windows setup, without WSL or Linux publish steps.

Trimmed builds are experimental and are not used by the release packaging scripts.
For a local Windows x64 comparison, publish from the repository root with:

```powershell
dotnet publish zgx/zg.csproj -c Release -r win-x64 --self-contained true -p:PublishTrimmed=true -o artifacts/trimmed/win-x64
$env:ZG_TEST_APP = (Resolve-Path artifacts/trimmed/win-x64/zg.exe).Path
python zgx/tests/archive_test_smoke.py
python zgx/tests/trimmed_publish_smoke.py
```

Set `ZG_BASELINE_APP` to an untrimmed executable to compare verbose ZIP metadata;
also set `ZG_TEST_DISK` to a disk image to compare its verbose metadata. The
trimmed build retains COM support and reflection-dependent archive and disk
assemblies, but linker warnings remain and additional platform and format
testing is required before enabling trimming in distributed packages.

Windows version 10.0.1 fixes coexistence with ZipGenius 6: `-install` places the
console directory first in the selected PATH, moving an existing entry forward
and removing duplicate forms. The system-wide MSI consequently takes priority
over the legacy command in PATH. Other PATH entries retain their order and text.
`-uninstall` removes only this console's directory, so the legacy command becomes
available again. Fully close and reopen the terminal application after setup;
already running processes retain their inherited environment. Explicit paths,
shell aliases and executables in the current directory keep their normal shell
precedence.

The machine-wide Windows MSI and DEB/RPM packages require administrator/root privileges. The additional current-user Windows MSI does not require administrator privileges.
Windows installs into `Program Files/ZipGenius X Console`; Linux into
`/opt/zipgenius/zgshell`, with a package-owned `/usr/bin/zg` symlink.
The executable is named `zg.exe` on Windows and `zg` on Linux.

The machine-wide MSI and DEB/RPM packages run `-install --system-wide --silent` after copying the payload,
and `-uninstall --system-wide --silent` before removing it. MSI actions run
elevated, check return codes, and have inverse rollback actions. Major MSI
upgrades remove the old package before installing/registering the new one.
Repair re-registers the path. Linux upgrades keep registration active and
run installation again for the new version; RPM only unregisters on final
removal. DEB unregisters on remove/deconfigure, including removal before purge.
The existing registration commands own their PATH entries; Linux does not
package `/etc/profile.d/zipgenius-x.sh` as a conffile.

Examples:

```powershell
msiexec /i zg-console-10.0.1.BUILD-win-x64.msi /L*v install.log
msiexec /x zg-console-10.0.1.BUILD-win-x64.msi /L*v uninstall.log
```

```sh
sudo apt install ./zg-console_10.0.0-1_amd64.deb
sudo apt remove zg-console
sudo dnf install ./zg-console-10.0.0-1.x86_64.rpm
sudo dnf remove zg-console
```

Linux payload targets glibc x64, with glibc >= 2.34, ICU, OpenSSL 3,
libgcc, libstdc++ and zlib installed. It is not an Alpine/musl package.
GUI SFX runtimes may need additional desktop libraries on the destination.
Do not install DEB and RPM builds into the same filesystem.

Verification scripts:

```powershell
./Verify-Msi.ps1
wsl -d Ubuntu -u root --exec python3 /mnt/c/path/to/zgx/packaging/verify-linux.py /mnt/c/path/to/zgx/packaging/dist
```

`Verify-Msi.ps1` inspects actual MSI database actions, arguments and ordering,
then performs an administrative extraction and executes the extracted payload.
It does not perform an elevated Windows installation. Linux verification needs
`bwrap` and uses disposable mounts for `/etc`, `/opt`, `/usr/bin`, the dpkg
database and logs. It checks installation, reinstall/upgrade, command execution,
permission failure codes, PATH cleanup and removal for both formats. RPM uses
`--nodeps` on this Debian-based test host; dependency resolution on an RPM-native
distribution remains a separate validation.

Validation of the 10.0.0 packages (27 October 2026): Windows/Linux self-contained
publishes succeeded; MSI database checks, administrative extraction and execution
passed; DEB/RPM installation, reinstall/upgrade, archive test/info commands,
SFX target listing, permission errors and uninstallation passed in isolated WSL
Ubuntu 26.04. The RPM test used `--nodeps`. A full elevated MSI transaction and
dependency resolution on a native RPM distribution have not been exercised.

The Windows PATH regression harness is run with:

```powershell
dotnet run --project ../../tools/WindowsPathRegistration.Tests -c Release
```

It simulates two distinct `zg.exe` programs in separate legacy/current folders
and checks real `where.exe` and `cmd.exe` resolution before install, after
promotion and after uninstall. It also covers repair of a previously appended
entry, idempotence, normalized duplicate entries and preservation of unrelated
PATH text. Tests change only child-process environments, without modifying the
machine or user PATH in the registry.

Version 10.0.1 passed this regression harness and the MSI database/extraction/
execution checks; the extracted zg.dll matches the published payload and reports
version 10.0.1.0. Full elevated install/upgrade testing remains to be done on Windows.

Version 10.0.2 simplifies startup without arguments to a version line, syntax,
command list, help references and one sentence about always-on ZipBomb protection.
The Italian startup output was checked against the requested text; detailed help
was exercised, and MSI database/extraction/execution checks passed again.

Subsequent builds use the main application's `10.0.*` numbering instead of the
temporary 10.0.0/10.0.1/10.0.2 packaging sequence. Website links in the console,
the HTML export template and Linux package metadata use https://www.zipgenius.it.

Release 10.0.9796.28757: all three packages were regenerated and checked. MSI
ProductVersion matches the assembly/file/startup version; the banner and embedded
HTML template use the corrected website. DEB/RPM installation, reinstall/upgrade,
startup version, command execution and removal passed in the isolated WSL test.
The MSI checks cover database actions, version/URL, extraction and execution;
elevated Windows transactions remain outside this validation.

Windows 10.0.9796.32050 adds Italian/English inline help for `zg -help -hexview`
and `zg -help hexview`. Invoking `zg -hexview` without a filename now displays
usage and exits with 99. The help routes and documented option list were checked.
DEB/RPM 10.0.9796.32050 were also regenerated with the same inline help.
Their isolated WSL install/reinstall/removal checks passed, including version
and installed `-hexview` help. RPM verification used `--nodeps` on Ubuntu.

Release 10.0.9796.34090 regenerates all formats without the `>> -command` help
debug line. It localizes over 70 additional messages/fragments, supports command
help with `--silent`, and skips manual page pauses when input/output is redirected.
Thirteen help routes, English/Italian hexview output, resource placeholders,
the archive integrity suite on Windows, MSI extraction/execution and Linux package
transactions were checked. See `../LOCALIZATION-AUDIT.md` for remaining legacy
English text and intentionally stable technical output. This is not a claim of
complete localization. MSI elevation and RPM-native dependency validation remain
subject to the limits documented above.

Release 10.0.9796.34632 revises the list help to describe metadata enumeration,
stored CRC values and risk reporting without blocking a readable list. The
`tests/list_metadata_smoke.py` regression passed on Windows and Linux.

Linux package presentation now uses Summary/Description `ZipGenius X Console`
and RPM Vendor/Packager `The ZipGenius Team`. The shared AppStream file is installed
under `/usr/share/metainfo/it.zipgenius.Console.metainfo.xml`, with the human-readable
name and developer, while the package identifier is now `zg-console`. Metadata validates
with `appstreamcli validate --no-net`. Software-center display has not been tested;
some clients use the technical name for a local RPM until AppStream catalog data
is loaded. Vendor/Packager alone do not guarantee the graphical author field.

`build-linux.py --release 2` creates a packaging-only revision without changing
the executable version. The verifier accepts `--release` or selects the highest
release for the requested version. Intermediate 10.0.9796.34090-2 packages are
superseded by 10.0.9796.34632-1.

Release 10.0.9796.35060 embeds all console help from `man/**/*.txt`, including
the existing full manuals. External `man` files are no longer published or read.
See `../man/README.md` for the comparison/merge decisions: 20 original files
unchanged, four additive merges, eight new pages. The staging RID folders are
checked and cleaned before publishing to remove obsolete loose files.

`tests/embedded_manuals_smoke.py` accepts a native application or assembly path
in `ZG_MANUAL_APP`. It copies the bundle into a temporary directory, ignores
external help and verifies all 14 command help pages plus startup against source
files. Windows/Linux and neutral/regional language fallback passed. Windows help
also passed from a single-file publish with only zg.exe copied to an isolated
folder. This does not assert every command has been tested in single-file mode.
All three installer packages were regenerated and verified as above.

Release 10.0.9796.35433 completes the Italian translation of `man/it-IT/add.txt`.
Command examples, option/value identifiers and page breaks were compared with
the previous file and preserved; the English file is unchanged. All help routes
and culture fallback checks passed on Windows/Linux, and all three packages were
regenerated. The Linux verifier now also checks Italian and English add help.

## Package rename

Installer filenames and DEB/RPM package identifiers now use `zg-console`. The command remains `zg`; installation locations and the MSI UpgradeCode are preserved. DEB declares Provides/Conflicts/Replaces for the former `zgshell` package; RPM declares Provides/Obsoletes and re-registers the command after the transaction so removal of the former package cannot leave its PATH registration absent. Descriptions explain user-facing functions and always-on ZipBomb protection. Existing `zgshell` artifacts in dist are historical; choose the new `zg-console` packages.

Version 10.0.9796.41133 was generated as zg-console MSI/DEB/RPM. Package names, descriptions and AppStream package identity were checked. MSI database/extraction/execution checks passed. Isolated Linux install/reinstall/removal and migration from the former zgshell identity passed for DEB and RPM. Migration fixtures were recreated from the preceding packaging scripts with the current executable payload and an older test package version; they are kept only under ignored work/rename-migration, not distributed. RPM tests still use --nodeps on Ubuntu; elevated Windows setup remains untested.

## Snap and Flatpak

Additional Linux x64 bundles use the same zg-console name and published assembly version. See [SANDBOX-PACKAGES.md](SANDBOX-PACKAGES.md) for build/install commands, runtime requirements, sandbox limits and validation. Use Build-SandboxPackages.ps1 for an existing Linux publish or Build-Packages.ps1 -IncludeSandboxPackages for all formats. No store publication is performed.

## Arch Linux

A local x86_64 zg-console package in .pkg.tar.zst format is generated with makepkg from the Linux self-contained publish. Use Build-ArchPackage.ps1 or Build-Packages.ps1 -IncludeArchPackage. See [ARCH-PACKAGE.md](ARCH-PACKAGE.md) for installation, dependencies, build and native isolated verification. No AUR or official repository publication is performed.

## Windows current-user installer

Windows packaging now emits one unified setup EXE by default; standalone machine-wide and current-user MSI variants remain available as advanced options. The additional `-win-x64-user.msi` installs under LocalAppData/Programs with limited privileges, calls -install/-uninstall without --system-wide and uses a separate UpgradeCode. Select `-WindowsScope unified|machine|user|both` when building; unified is the default. See [WINDOWS-USER-INSTALLER.md](WINDOWS-USER-INSTALLER.md) for installation, scope coexistence and verification. The Linux package scopes are unchanged.

## Unified Windows setup

The default Windows distribution is now one native x64 setup EXE with scope selection, current-user default, elevation only for machine-wide operations and preservation of an existing MSI scope. Compatible with both preceding MSI upgrade identities. Internal MSI files are staged under work/windows-msi. See [WINDOWS-UNIFIED-SETUP.md](WINDOWS-UNIFIED-SETUP.md) for usage, maintenance, CLI options, build requirements and verification limits. Historical standalone MSI packages remain available.
