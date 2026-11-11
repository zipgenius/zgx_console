# Extraction validation

Validated locally on Windows and Ubuntu 26.04 under WSL:

* Standalone .NET 10 build, including automatic portable SFX generation.
* Consecutive builds produced 10.0.1.2 and 10.0.1.3.
* Windows self-contained portable ZIP.
* Machine and current-user MSI, plus the native unified setup EXE.
* MSI structure, action arguments, sequence, extracted payload startup and embedded SFX resources verified with Verify-Msi.ps1.
* Embedded manuals and archive-integrity command integration tests on Windows.
* Linux x64 self-contained publish, embedded-manual tests and DEB/RPM package generation on Ubuntu WSL.
* GitHub Actions workflows checked with actionlint.
* Parent repository shared SFX core build and Windows PATH registration tests pass after detachment.

Hosted GitHub Actions, ARM64 and macOS execution have not run: this repository has no remote yet. Native binary licensing/provenance notes are in THIRD_PARTY_NOTICES.md. Existing compiler/NuGet warnings remain; compilation succeeds.

## SQX inclusion

Windows x64 package 10.0.1.6 includes all five original x86/x64 SQX runtime and bridge DLLs. A native SQX create/extract roundtrip passed and preserved file contents. The project maintainer confirmed inclusion and redistribution. SQX is excluded from ARM64 and non-Windows publish payloads.
