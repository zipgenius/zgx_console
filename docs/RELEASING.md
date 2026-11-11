# Releasing

## Automated builds

`.github/workflows/build-release.yml` runs on pushes to `main`, pull requests,
version tags (`v*`) and manual dispatch. It builds six portable packages for
Windows, Linux and macOS (x64 and ARM64), Windows x64 MSI/setup, and Linux x64
DEB/RPM packages. Smoke tests verify embedded manuals and archive-integrity
commands on native runners; the cross-built Windows ARM64 package is not executed.
Build artifacts are retained for 14 days.

The reusable `linux-extra.yml` workflow also builds Arch, Snap and Flatpak
packages on every run, using the same build number. It does not publish to stores.

## Versions and tags

The current product version is `10.0.1`. CI allocates a shared build revision as
`github.run_number * 10 + github.run_attempt`, producing `10.0.1.BUILD`.
The workflow accepts up to nine attempts per run and revisions up to 65534;
it fails explicitly if either limit is exceeded.

Release tags must be `v10.0.1` or `v10.0.1.BUILD`. In the four-field form,
`BUILD` must equal the revision allocated to that tag's workflow run.
An unsupported tag fails validation.

Pushing a supported tag creates a GitHub release after all package jobs succeed.
The release contains all distribution packages and `SHA256SUMS`. Manual runs,
branch pushes and pull requests create build artifacts without publishing a release.
Only the release job has repository write permission; pull requests cannot publish.

## Third-party components

Before publishing a release, review [THIRD_PARTY_NOTICES.md](../THIRD_PARTY_NOTICES.md),
including the source obligations for the inherited 7-Zip binaries.

## Ubuntu build dependencies

Linux packaging jobs use `build/install-ubuntu-tools.sh` to install their tools.
The helper tries HTTPS Ubuntu mirrors in order (MIT, kernel.org, then the Ubuntu
archive), with bounded timeouts and retries. Each mirror must provide all required
release indexes before package installation starts. APT verifies the normal Ubuntu
archive signatures. The helper uses temporary source/list files and leaves the
runner's existing APT sources unchanged. It is also used inside the Snap container.
`ZG_UBUNTU_MIRRORS` can override the mirror list with one HTTPS URL per line.
