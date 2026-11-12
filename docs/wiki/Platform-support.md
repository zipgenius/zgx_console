# Platform support and troubleshooting

[Home](Home) · [Installation](Installation) · [Usage](Usage)

## Formats and runtimes

| Feature | Support and limits |
| --- | --- |
| Portable packages | Windows, Linux and macOS; x64 and ARM64 build targets |
| ZIP | Managed engines; ZIP integrity testing includes AES-encrypted ZIPs |
| Other archive tests | Windows uses 7-Zip where available; Linux/macOS use managed readers for recognised formats, including 7Z, RAR, TAR and GZIP |
| SQX | Native libraries/bridges shipped for Windows x64, including x86 helpers; unsupported on ARM64/Linux/macOS; `-test` is not implemented and returns 7 |
| CAB creation | Requires Windows components; do not assume Linux/macOS support |
| Linux 7Z/TAR.XZ conversion output | Requires `libarchive.so.13` and locale `C.UTF-8`; no encrypted or split 7Z output |
| macOS native formats | Included Windows/Linux 7-Zip libraries do not provide a macOS native backend; a macOS build does not imply complete format parity |
| Disk information | Managed readers inspect ISO/UDF, VHDX, VHD, VMDK, VDI, DMG and RAW; some variants, encryption and missing parent/extents can prevent a full report |
| SFX | Only templates embedded in that build are available; inspect `zg -sfx --list-targets`; creating/signing macOS packages requires a Mac |

Creating, extracting, listing, testing and converting the same extension can use different engines. A format readable by one command is not necessarily writable or testable by another. For example, TAR has no per-file data checksum, so a successful integrity test cannot detect every content change. Use the command's embedded help for its specific scope.

## ZIP protection

ZipBomb checks are always enabled for ZIPs and ZIP-based files, including renamed ZIPs. High risk blocks operations that decompress or package data; an incomplete inspection also blocks them as a precaution. A readable metadata list can still be displayed with a risk code. `--silent` and `--yes` do not hide security diagnostics or override a block.

Codes `42` and `43` mean high risk and incomplete inspection respectively. `-list` and completed ZIP information reports can also return `41` for a warning. A moderate warning followed by successful extraction, addition, backup, conversion or testing returns `0`.

An incomplete check does not prove the archive is malicious. Ask the sender for a fresh or trusted copy when blocked. These checks are not a virus scan; a successful test is not a guarantee that running the extracted contents is safe. See [ZIP security details](https://github.com/zipgenius/zgx_console/blob/main/zgx/ZIP-SECURITY.md).

## Common problems

| Symptom | What to check |
| --- | --- |
| `zg` not found after setup | Fully restart the terminal application; inspect PATH and use the full executable path |
| Wrong version runs | Windows: `Get-Command zg -All`, `where.exe zg`; Unix: `command -v zg`; inspect aliases and current directory; run `zg -yourpath` |
| Permission denied on Unix | Verify executable permission (`chmod +x zg`), filesystem mount policy and destination permissions |
| Missing native library / unsupported operation | Keep the complete distribution together; verify CPU, system dependencies and the format limits above |
| No password prompt | `--silent` or redirected input disables interactive prompting; supply the password explicitly when automating |
| Code 7 | The current engine, platform or sandbox does not support the operation; SQX testing is not available |
| Code 9 from `-info` | The metadata report is incomplete; keep required disk parents/extents and check reader limitations |
| Codes 42/43 | Read the security diagnostic; there is no flag to bypass the block |
| Snap cannot see files | Strict confinement limits hidden files/system paths; connect removable-media when appropriate |
| Flatpak path unavailable | Use absolute paths within the sandbox's access permissions; device access is limited |
| SFX target missing | Run `zg -sfx --list-targets`; use an included target or build the needed templates |

Packages are unsigned. Follow your organisation's software approval policy. Checksums establish whether files match the published values; they do not replace platform signing or malware scanning.

## Italiano

Le distribuzioni portabili hanno target Windows, Linux e macOS, x64 e ARM64, ma i formati non hanno supporto identico. Un formato leggibile può non essere scrivibile o verificabile dallo stesso comando.

- ZIP usa motori gestiti; il test comprende ZIP AES. Su Linux/macOS i test degli altri archivi usano i lettori gestiti disponibili.
- SQX richiede i componenti nativi forniti per Windows x64, con helper x86. Non è supportato su ARM64, Linux o macOS; `-test` SQX restituisce `7` anche su Windows.
- La creazione CAB richiede componenti Windows. Su Linux la conversione verso 7Z/TAR.XZ richiede `libarchive.so.13` e `C.UTF-8`; non crea 7Z cifrati o divisi in volumi.
- La presenza di una build macOS non implica supporto completo dei backend nativi Windows/Linux.
- I modelli SFX dipendono dalla build: controlla `zg -sfx --list-targets`. Per creare e firmare localmente un pacchetto macOS serve un Mac.
- Le informazioni sulle immagini disco non montano i file. Varianti non supportate, cifratura o componenti mancanti possono produrre un rapporto incompleto, codice `9`.

La protezione ZipBomb è sempre attiva per ZIP e file basati su ZIP, anche rinominati. `42` indica rischio alto e `43` controllo incompleto: le operazioni sui dati vengono bloccate. Un elenco leggibile può comunque essere mostrato con il relativo codice di rischio; `41` indica un avviso. Per le operazioni consentite un avviso moderato non cambia il codice `0` se il lavoro riesce. `--silent` e `--yes` non disattivano la protezione. Un controllo incompleto non prova che il file sia malevolo: chiedi una copia nuova o attendibile. Non è una scansione antivirus.

Se `zg` non viene trovato, riapri completamente il terminale e verifica il PATH. Se parte un'altra versione, controlla `Get-Command zg -All` e `where.exe zg` su Windows, oppure `command -v zg` su Unix; usa `zg -yourpath` e, se necessario, il percorso completo. Su Unix verifica il permesso di esecuzione e i permessi della destinazione. Mantieni tutti i file della distribuzione e controlla le dipendenze di sistema.

Con `--silent` o input reindirizzato non vengono chieste password. Snap limita file nascosti e percorsi di sistema; per supporti rimovibili usa l'interfaccia dedicata. Con Flatpak preferisci percorsi assoluti accessibili nel sandbox. I pacchetti sono non firmati: segui le regole della tua organizzazione per l'installazione.
