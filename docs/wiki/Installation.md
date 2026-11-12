# Installation

[Home](Home) · [Italiano](Installazione) · [Usage](Usage)

## Choose and verify a download

Get a package from [Releases](https://github.com/zipgenius/zgx_console/releases), or download an artifact from a successful [Actions run](https://github.com/zipgenius/zgx_console/actions) if there is no release. Extract the outer Actions artifact ZIP before installing its contents. Artifacts expire after 14 days. Do not assume every workflow run completed every package.

Use the operating system and processor of the destination computer: `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64` (Intel Mac), or `osx-arm64` (Apple Silicon). Installers and DEB/RPM/Arch/Snap/Flatpak packages currently target x64; use portable packages for ARM64. See [platform limitations](Platform-support).

The packages are self-contained: no separate .NET installation is needed. Preserve the entire extracted folder, including native libraries, SFX templates embedded in assemblies and license files. Linux still needs its system libraries; GUI SFX applications may need desktop libraries.

Replace `VERSION`, `BUILD` and sample package names below with the actual downloaded filename. Published releases include `SHA256SUMS`. Compare a downloaded file's hash with its entry:

```powershell
Get-FileHash .\zg-console-VERSION-win-x64.zip -Algorithm SHA256
```

```sh
sha256sum zg-console-VERSION-linux-x64.tar.gz
# macOS:
shasum -a 256 zg-console-VERSION-osx-arm64.tar.gz
```

## Windows setup (x64)

Run `zg-console-VERSION-win-x64-setup.exe` normally. Choose:

- **Current user** (default): `%LOCALAPPDATA%\Programs\ZipGenius X Console`, user PATH, no elevation.
- **All users**: Program Files, system PATH, administrator authorization required.

Fully close and reopen the terminal application, then run `zg -help`. The setup preserves an existing installation's scope. To change scope, remove that installation first. Use the setup again to repair, update or uninstall, or remove the application in Windows Settings.

For automation in PowerShell:

```powershell
.\zg-console-VERSION-win-x64-setup.exe /quiet /currentuser
# System installation; Windows may request elevation:
.\zg-console-VERSION-win-x64-setup.exe /quiet /allusers
# Maintenance of the detected scope:
.\zg-console-VERSION-win-x64-setup.exe /quiet /repair
.\zg-console-VERSION-win-x64-setup.exe /quiet /uninstall
```

Choose one install command. Specify `/currentuser` or `/allusers` for maintenance if both scopes exist. `/quiet` does not suppress UAC. Codes include `0` success and `3010` success requiring restart. Standalone machine/user MSI packages are advanced alternatives; do not install both simply to obtain the same command. See [setup details](https://github.com/zipgenius/zgx_console/blob/main/zgx/packaging/WINDOWS-UNIFIED-SETUP.md).

## Windows portable (x64 or ARM64)

Extract the whole ZIP into a permanent folder. In PowerShell, from that folder:

```powershell
.\zg.exe -help
.\zg.exe -install
```

`-install` registers the current application folder in the user PATH. For the system PATH, use an elevated terminal and `.\zg.exe -install --system-wide`. It does not copy the program or create an MSI installation. Reopen the terminal afterwards. Before moving or deleting a registered folder, run:

```powershell
.\zg.exe -uninstall
# Only if registered system-wide, from an elevated terminal:
.\zg.exe -uninstall --system-wide
```

Then remove the extracted folder if no longer needed. Use the same scope used at installation. `-uninstall` removes PATH registration, not application files.

## Linux system packages (x64)

Install the format for your distribution; use one package manager, not DEB and RPM in the same filesystem:

```sh
# Debian / Ubuntu:
sudo apt install ./zg-console_VERSION-1_amd64.deb
# Fedora / compatible RPM system:
sudo dnf install ./zg-console-VERSION-1.x86_64.rpm
# Arch Linux:
sudo pacman -U ./zg-console-VERSION-1-x86_64.pkg.tar.zst
```

These install into `/opt/zipgenius/zgshell` and own `/usr/bin/zg`. PATH registration is managed by package scripts; do not run `-install` manually for these packages. Verify with `zg -help`.

The Linux payload requires glibc >= 2.34, ICU, OpenSSL 3, libgcc, libstdc++ and zlib. It does not target Alpine/musl. Linux conversion to 7Z/TAR.XZ additionally needs `libarchive.so.13` and the `C.UTF-8` locale. Package managers resolve declared dependencies using configured repositories.

Update by installing the newer package with the same command. Remove using the matching manager:

```sh
sudo apt remove zg-console
sudo dnf remove zg-console
sudo pacman -R zg-console
```

## Linux and macOS portable

Extract the tar.gz into a permanent location. Example for a Linux x64 user installation:

```sh
mkdir -p "$HOME/.local/opt"
tar -xzf zg-console-VERSION-linux-x64.tar.gz -C "$HOME/.local/opt"
cd "$HOME/.local/opt/zg-console-VERSION-linux-x64"
chmod +x zg
./zg -help
./zg -install
```

On macOS use the appropriate `osx-x64` or `osx-arm64` archive and its matching extracted directory instead. `-install` appends the application directory to `~/.bashrc` on Linux or `~/.zprofile` on macOS. Start a new Bash session on Linux or a new login Zsh session on macOS. For other shells, add the directory to PATH in that shell's own configuration.

For a system-wide portable installation, first place the entire distribution in an administrator-managed permanent directory, then run `sudo ./zg -install --system-wide` from it. This writes `/etc/profile.d/zipgenius-x.sh` on Linux or `/etc/paths.d/zipgenius-x` on macOS. Start a new login session. Do not register a user-writable directory in the system PATH.

Before moving or removing the directory, run `./zg -uninstall` or `sudo ./zg -uninstall --system-wide`, matching its registration scope. Then remove the extracted files. To update a portable copy, unregister the old directory, extract the new distribution into its permanent directory, and register the new directory. Do not replace only the executable.

## Snap and Flatpak (Linux x64)

These are local bundles, not Snap Store or Flathub publications.

```sh
sudo snap install --dangerous ./zg-console_VERSION_amd64.snap
snap run zg-console -help
# Optional removable-media access:
sudo snap connect zg-console:removable-media
```

`--dangerous` allows installing a local bundle without a Snap Store signature; it does not disable ZIP checks. Snap uses core26 and strict confinement; hidden files and system paths are generally unavailable. The direct command is `zg-console`. Optionally use `sudo snap alias zg-console zg`, choosing which copy should own that name. Remove with `sudo snap remove zg-console`.

```sh
flatpak --user remote-add --if-not-exists flathub https://dl.flathub.org/repo/flathub.flatpakrepo
flatpak --user install ./zg-console-VERSION-x86_64.flatpak
flatpak run it.zipgenius.Console -help
```

Flatpak needs the Freedesktop Platform 25.08 runtime, obtained separately via Flatpak. Use absolute paths for input/output, for example `flatpak run it.zipgenius.Console -info "$HOME/Downloads/image.iso"`. The bundle requests host filesystem access within sandbox and user permissions, not general device access. Remove with `flatpak --user uninstall it.zipgenius.Console`.

Snap/Flatpak do not use host PATH registration: `-install` and `-uninstall` return code `7`. Use their package manager. See [sandbox details](https://github.com/zipgenius/zgx_console/blob/main/zgx/packaging/SANDBOX-PACKAGES.md).

## Verify command resolution

```powershell
Get-Command zg -All
where.exe zg
zg -yourpath
zg -help
```

```sh
command -v zg
zg -yourpath
zg -help
```

If another ZipGenius version is selected, check aliases, the current directory and PATH order. A Windows user PATH entry does not override a competing machine PATH entry in every environment. Invoke the intended executable by full path when necessary. See [troubleshooting](Platform-support).
