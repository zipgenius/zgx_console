# ZipGenius X Console

Standalone command-line archive utility, extracted from ZipGenius X. Initial product version: **10.0.1**. License: **GPL-3.0-only**, with separately licensed dependencies.

## Install and use

Download a package matching your operating system and CPU from [Releases](https://github.com/zipgenius/zgx_console/releases). If no release is available, use a successful run in [Actions](https://github.com/zipgenius/zgx_console/actions); build artifacts expire after 14 days. Distributed packages include .NET; the SDK is only needed to build from source.

- **Windows x64:** run the unified `-win-x64-setup.exe`. Choose current-user installation (default) or all users (administrator required). Fully close and reopen your terminal afterwards.
- **Linux x64:** install the downloaded DEB with `sudo apt install ./package.deb`, RPM with `sudo dnf install ./package.rpm`, or Arch package with `sudo pacman -U ./package.pkg.tar.zst`. Replace `package` with the actual filename. The command is `zg`.
- **Portable Windows/Linux/macOS, x64/ARM64:** extract the entire ZIP or tar.gz into a permanent folder. Run `zg.exe` on Windows or `./zg` on Unix from that folder. `-install` registers that folder in PATH; it does not copy files. Keep the whole distribution together.
- **Snap/Flatpak:** use the local bundles; they are not published to Snap Store or Flathub. Commands and sandbox restrictions are described in the installation guide.

See the **[Wiki](https://github.com/zipgenius/zgx_console/wiki)** for [installation and removal](https://github.com/zipgenius/zgx_console/wiki/Installation), [usage examples](https://github.com/zipgenius/zgx_console/wiki/Usage), and [platform support and troubleshooting](https://github.com/zipgenius/zgx_console/wiki/Platform-support). Guide in italiano: **[Installazione](https://github.com/zipgenius/zgx_console/wiki/Installazione)** e **[Esempi d'uso](https://github.com/zipgenius/zgx_console/wiki/Esempi)**. Wiki sources are versioned in [docs/wiki](docs/wiki).

```sh
zg -help
zg -add documents.zip "+documents/*" R1 F1 C5
zg -list documents.zip --verbose
zg -test documents.zip
zg -extract documents.zip extracted R1 F1 O1
zg -hash documents.zip --sha256
```

Run these examples in a working folder containing `documents`. Quoting the entire `+pattern` prevents shell wildcard expansion. `O1` skips existing files during extraction; `O0` overwrites them. Use `zg -help -add` or `zg -help -extract` for details. ZIP safety checks are always active; `--silent` and `--yes` do not bypass them. Supported formats depend on the platform and command.

## Build

Install .NET SDK 10, then run:

```sh
dotnet build zgx/zg.csproj -c Release
dotnet run --project zgx/zg.csproj -- -help
```

SFX terminal and desktop templates are built automatically for the current runtime. The first build restores NuGet packages and publishes self-contained SFX templates; network access is required. Everything needed is inside this repository.

Every local build allocates the next revision in `.build/build-number.txt`, starting at 1: `10.0.1.1`, `10.0.1.2`, etc. Run local builds serially. To assign a common number to multiple platforms, pass `-p:BuildNumber=N`. The counter is local, ignored by Git, and independent across clones. Revisions must be 1..65534; the target fails explicitly on overflow. CI uses `github.run_number * 10 + github.run_attempt`, shared across its platform jobs, so reruns get a new revision (up to nine attempts per run). The workflow rejects exhaustion instead of wrapping.

## Distributions

```sh
python build/package.py --rid win-x64 --build-number 1
python build/package.py --rid linux-x64 --build-number 1
python build/package.py --rid osx-arm64 --build-number 1
```

Run on the matching operating system. Output: `artifacts/dist`. Portable Windows ZIP and Linux/macOS tar.gz distributions support x64 and arm64. The native archive backends are platform dependent: SQX runtime libraries and bridges are included for Windows x64 (including the required x86 helpers); SQX is unsupported on ARM64, Linux and macOS. The inherited 7-Zip shared libraries are Windows/Linux binaries. macOS builds do not imply full native format support; libarchive writing currently targets Linux. See `zgx/LINUX.md` and embedded manuals for command behavior.

Windows x64 MSI and unified setup (machine/user selection), Linux x64 DEB/RPM, Arch, Snap and Flatpak scripts are retained in `zgx/packaging`. Windows setup requires Visual Studio C++ and Windows SDK. Linux DEB/RPM requires `dpkg-deb` and `rpmbuild`; Arch requires `makepkg`, `pacman` and `zstd`. Snap requires Ubuntu 26.04 and Flatpak requires the Freedesktop runtime noted in its script.

## Builds and releases

GitHub Actions automatically builds packages for Windows, Linux and macOS.
Build artifacts are available from the Actions tab for 14 days.

Pushing a supported version tag creates a GitHub release containing
the distribution packages and SHA-256 checksums.

See [Releasing](docs/RELEASING.md) for workflow and version-tag details.
Third-party component information is available in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
