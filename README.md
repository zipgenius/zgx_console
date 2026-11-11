# ZipGenius X Console

Standalone command-line archive utility, extracted from ZipGenius X. Initial product version: **10.0.1**. License: **GPL-3.0-only**, with separately licensed dependencies.

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

## GitHub Actions and releases

`build-release.yml` runs on pushes to main, pull requests, version tags and manual dispatch. It builds six portable packages, Windows x64 MSI/setup, and Linux x64 DEB/RPM; smoke tests verify startup and embedded manuals. Artifacts are retained for 14 days. Tags must match `v10.0.1` or `v10.0.1.BUILD` (the latter must match the allocated CI build). A successful tag build creates a GitHub release with every package and SHA256SUMS. Manual runs create packages without publishing a release. Only the release job has write permission; pull requests cannot publish.

The reusable `linux-extra.yml` workflow also builds Arch, Snap and Flatpak on every run using the same build number, without publishing to stores. All packages are included in tag releases.

Before publishing, review `THIRD_PARTY_NOTICES.md`, particularly the inherited 7-Zip binary source obligations. No remote or GitHub repository is configured by the extraction.
