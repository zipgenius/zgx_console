# Maintaining the GitHub Wiki

User documentation is maintained in `docs/wiki/*.md` and published to the separate [GitHub Wiki repository](https://github.com/zipgenius/zgx_console/wiki). Keep the English and Italian guides consistent. `_Sidebar.md` supplies navigation; `Home.md` is the entry point.

Review commands against `zgx/man`, platform code and packaging scripts. Use version placeholders in installation examples so documentation does not direct users to obsolete builds. Verify examples in a disposable working directory without modifying the host PATH or installing packages just to test documentation.

After committing documentation changes to the main repository, publish the same Markdown files to an existing wiki checkout:

```powershell
git clone https://github.com/zipgenius/zgx_console.wiki.git .build/wiki
Copy-Item docs/wiki/*.md .build/wiki/
git -C .build/wiki add -- '*.md'
git -C .build/wiki commit -m "Update installation and usage documentation"
git -C .build/wiki push
```

For subsequent updates, use `git -C .build/wiki pull --ff-only` instead of cloning. Inspect differences before committing, especially if Wiki pages were edited on GitHub. This is a manual publishing workflow: changing `docs/wiki` alone does not update the live Wiki. No automatic Wiki synchronization is configured.

## Documentation validation — 12 November 2026

Checked links between all seven Wiki Markdown files and balanced fenced code blocks. Ran 16 command examples against the existing Windows x64 self-contained `10.0.1.6` payload in a temporary directory: ZIP creation/addition, list, integrity test, extraction, filters, AES-256 creation/test, timestamped and updating backups, SHA-256/SHA-512, TAR.GZ conversion, SFX target listing and Windows terminal SFX creation. Verified nested extraction, temporary-file exclusions and SHA-256 against Python. The created SFX was not launched. No host PATH changes or package installations were performed.

An additional ZIP-to-7Z conversion failed with code 99 on a directory entry. The usage pages document that observed limitation and use the passing TAR.GZ example. Linux/macOS examples and installation commands were reviewed against source and packaging documentation, not executed during this documentation update. This validation does not replace platform/package test suites.
