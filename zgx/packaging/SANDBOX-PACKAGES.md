# Snap e Flatpak â€” zg-console

Questi pacchetti Linux x64 usano il publish self-contained della console: .NET Ã¨ incluso. Non sono pubblicati negli store. Il nome dei pacchetti Ã¨ `zg-console`; il nome dell'app Flatpak Ã¨ `it.zipgenius.Console`.

## Installazione Snap

```sh
sudo snap install --dangerous ./zg-console_10.0.9796.41133_amd64.snap
snap run zg-console -help
```

`--dangerous` serve per installare il file locale senza firma dello Snap Store; non cambia la protezione ZipBomb. La base core26 viene scaricata da snapd se manca. Il pacchetto Ã¨ strict: puÃ² lavorare sui file personali visibili dell'utente. I file nascosti e le cartelle di sistema non sono generalmente accessibili. Per supporti rimovibili e percorsi sotto /mnt:

```sh
sudo snap connect zg-console:removable-media
```

Il comando diretto Ã¨ `zg-console`. Se vuoi chiamarlo `zg`, crea esplicitamente un alias, scegliendo questa installazione quando sono presenti anche DEB/RPM:

```sh
sudo snap alias zg-console zg
sudo snap remove --purge zg-console
```

## Installazione Flatpak

```sh
flatpak --user remote-add --if-not-exists flathub https://dl.flathub.org/repo/flathub.flatpakrepo
flatpak --user install ./zg-console-10.0.9796.41133-x86_64.flatpak
flatpak run it.zipgenius.Console -help
```

Il runtime Freedesktop Platform 25.08 viene installato separatamente da Flatpak. La console include .NET. Il pacchetto consente l'accesso ai file dell'utente tramite `--filesystem=host`, nei limiti del sandbox e dei permessi dell'utente. Non concede accesso generale ai dispositivi. Per i percorsi che attraversano il sandbox usa nomi assoluti, per esempio:

```sh
flatpak run it.zipgenius.Console -info "$HOME/Downloads/disco.iso" --verbose
flatpak --user uninstall it.zipgenius.Console
```

Ãˆ possibile creare un alias nella propria shell: `alias zg='flatpak run it.zipgenius.Console'`.

## Comportamento

Le protezioni ZipBomb restano sempre attive. I pacchetti non modificano il profilo della shell e non eseguono le registrazioni degli installer DEB/RPM: `-install` e `-uninstall` restituiscono 7 e indicano di usare il gestore dei pacchetti. `-yourpath` puÃ² mostrare il percorso interno, che non va aggiunto al PATH dell'host.

Le operazioni sui file e la creazione SFX sono disponibili entro i permessi del sandbox. Le funzioni che richiedono accesso a dispositivi, percorsi riservati o programmi esterni possono avere limiti rispetto ai pacchetti DEB/RPM. Non Ã¨ una dichiarazione di paritÃ  con una normale installazione sul sistema. La creazione di un SFX non richiede di eseguirlo nel sandbox.

## Rigenerazione

Richiede WSL Ubuntu 26.04 per Snap core26, Python 3, snapd, squashfs-tools e Flatpak. In WSL:

```sh
sudo apt install flatpak squashfs-tools snapd
flatpak --user remote-add --if-not-exists flathub https://dl.flathub.org/repo/flathub.flatpakrepo
flatpak --user install flathub org.freedesktop.Platform/x86_64/25.08
```

Dalla cartella ZipGenius X in PowerShell, dopo aver prodotto il publish Linux:

```powershell
& ../zgx/packaging/Build-SandboxPackages.ps1 -Distribution Ubuntu-26.04
# Solo un formato:
& ../zgx/packaging/Build-SandboxPackages.ps1 -Distribution Ubuntu-26.04 -Format flatpak
# Nuovo publish e tutti i formati:
& ../zgx/packaging/Build-Packages.ps1 -Distribution Ubuntu-26.04 -IncludeSandboxPackages
```

Lo script legge la versione dall'assembly pubblicato, genera i file in dist e aggiorna SHA256SUMS. I file temporanei vengono creati sul filesystem Linux e rimossi alla fine. `build-sandbox.py` puÃ² essere usato direttamente su Linux, indicando `--payload`, `--output`, `--version` e facoltativamente `--format`/`--runtime`. La preparazione Snap incorpora le librerie native .NET non fornite dalla base, prese da Ubuntu 26.04; il pacchetto Flatpak usa quelle del runtime Freedesktop.

## Verifica

Dopo l'installazione locale:

```sh
python3 verify-sandbox.py --kind snap --version 10.0.9796.41133
python3 verify-sandbox.py --kind flatpak --version 10.0.9796.41133
```

La suite usa file temporanei visibili nella home, verifica startup, help incorporato, aggiunta/elenco/test/info/estrazione, creazione SFX e rifiuto delle registrazioni manuali. Non modifica alias nÃ© profili della shell.

Riferimenti: [formato Snap](https://snapcraft.io/docs/reference/development/yaml-schemas/the-snap-format/), [comandi Flatpak](https://docs.flatpak.org/en/latest/flatpak-command-reference.html).

## Risultati della build 10.0.9796.41133

Generati `zg-console_10.0.9796.41133_amd64.snap` e `zg-console-10.0.9796.41133-x86_64.flatpak`. Checksum verificati insieme agli altri formati.

Entrambi installati ed eseguiti su WSL Ubuntu 26.04. Superati startup/versione, help incorporato, aggiunta, elenco, integrità, informazioni, estrazione, creazione di SFX terminale Linux, blocco ZipBomb senza creare la destinazione e rifiuto di -install/-uninstall anche con maiuscole. Le installazioni di prova sono state rimosse. Nessun alias o profilo della shell modificato; gli strumenti di build e i runtime di supporto restano disponibili nell'ambiente WSL.

Le prove non equivalgono a un collaudo di tutte le distribuzioni Linux, dei gestori grafici, delle policy AppArmor su un sistema nativo o alla revisione degli store. I pacchetti sono locali, senza pubblicazione su Snap Store o Flathub. I log ignorati `packaging-snap-*` e `packaging-flatpak-*` sono nella cartella zgx.
