# ZipGenius X Console Wiki

ZipGenius X Console is a standalone command-line tool for creating, extracting, listing and checking archives, making ZIP backups, calculating hashes and inspecting disk images. The command is `zg` (`zg.exe` on Windows).

## English

1. [Installation](Installation): choose a package, install system-wide or for one user, register a portable copy, update and uninstall.
2. [Usage](Usage): practical archive, backup, hash, conversion and SFX examples, including exit codes for scripts.
3. [Complete command reference](Command-reference): all commands, options and examples from the English HTML guide.
4. [Platform support](Platform-support): native format limits, security checks and troubleshooting.

## Italiano

1. [Installazione](Installazione): pacchetti, installazione nel sistema o personale, copia portabile, aggiornamento e rimozione.
2. [Esempi d'uso](Esempi): archivi, backup, hash, conversione, SFX e codici per gli script.
3. [Guida completa](Guida-completa): tutti i comandi, le opzioni e gli esempi della guida HTML italiana.
4. [Piattaforme e problemi comuni](Platform-support#italiano): limiti dei formati e protezione ZipBomb.

## Downloads and source

- [Releases](https://github.com/zipgenius/zgx_console/releases) — published packages and SHA-256 checksums.
- [Actions](https://github.com/zipgenius/zgx_console/actions) — packages from successful builds, retained for 14 days. A build artifact is not a published release.
- [Repository and build instructions](https://github.com/zipgenius/zgx_console).
- [Release workflow](https://github.com/zipgenius/zgx_console/blob/main/docs/RELEASING.md).

Packages include .NET. Building from source requires .NET SDK 10. The current product series is `10.0.1.BUILD`; examples use placeholders instead of pinning an old build. Availability of a particular package depends on the selected release or workflow run.

The Wiki sources are maintained in [docs/wiki](https://github.com/zipgenius/zgx_console/tree/main/docs/wiki). Detailed command help is embedded in the executable: `zg -help`, `zg -help -add`, `zg -help -exitcodes`.
