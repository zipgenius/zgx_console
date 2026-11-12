# Installazione

[Home](Home) · [English](Installation) · [Esempi d'uso](Esempi)

## Download e requisiti

Scarica il pacchetto da [Releases](https://github.com/zipgenius/zgx_console/releases). Se non è presente una release, apri una compilazione riuscita in [Actions](https://github.com/zipgenius/zgx_console/actions) e scarica il relativo artefatto: viene conservato per 14 giorni. Estrai prima lo ZIP esterno dell'artefatto. La disponibilità dei pacchetti dipende dai job completati nella compilazione scelta.

Scegli sistema e processore del computer destinatario: `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64` per Mac Intel, `osx-arm64` per Apple Silicon. Gli installer e i pacchetti DEB/RPM/Arch/Snap/Flatpak sono attualmente x64; per ARM64 usa la distribuzione portabile. Consulta i [limiti delle piattaforme](Platform-support#italiano).

I pacchetti includono .NET. Per usarli non serve l'SDK, richiesto invece per compilare i sorgenti. Mantieni insieme tutti i file della distribuzione. Su Linux restano necessarie le librerie di sistema; gli SFX grafici possono richiedere librerie desktop aggiuntive.

Negli esempi sostituisci `VERSION` con la versione completa e usa il nome effettivo del file scaricato. Le release pubblicate includono `SHA256SUMS`: confronta l'hash del pacchetto con la sua voce nel file.

```powershell
Get-FileHash .\zg-console-VERSION-win-x64.zip -Algorithm SHA256
```

```sh
sha256sum zg-console-VERSION-linux-x64.tar.gz
# macOS:
shasum -a 256 zg-console-VERSION-osx-arm64.tar.gz
```

## Windows: installer x64

Apri normalmente `zg-console-VERSION-win-x64-setup.exe` e scegli:

- **Solo per me**, predefinito: `%LOCALAPPDATA%\Programs\ZipGenius X Console`, PATH utente, senza elevazione.
- **Per tutti gli utenti del computer**: Program Files e PATH di sistema; richiede autorizzazione amministrativa.

Chiudi e riapri completamente il terminale, poi esegui `zg -help`. Il setup conserva l'ambito di un'installazione esistente. Per cambiarlo, rimuovi prima quella copia. Riapri il setup per riparare, aggiornare o disinstallare; puoi rimuovere il programma anche dalle Impostazioni di Windows.

Automazione in PowerShell, scegliendo un solo comando di installazione:

```powershell
.\zg-console-VERSION-win-x64-setup.exe /quiet /currentuser
.\zg-console-VERSION-win-x64-setup.exe /quiet /allusers
# Manutenzione dell'ambito rilevato:
.\zg-console-VERSION-win-x64-setup.exe /quiet /repair
.\zg-console-VERSION-win-x64-setup.exe /quiet /uninstall
```

Se esistono entrambi gli ambiti, specifica `/currentuser` o `/allusers` anche per la manutenzione. `/quiet` non sopprime UAC. Tra i codici: `0` successo, `3010` successo con riavvio necessario. Gli MSI separati macchina/utente sono alternative avanzate al setup. [Dettagli dell'installer](https://github.com/zipgenius/zgx_console/blob/main/zgx/packaging/WINDOWS-UNIFIED-SETUP.md).

## Windows: copia portabile x64 o ARM64

Estrai tutto lo ZIP in una cartella permanente. Da quella cartella, in PowerShell:

```powershell
.\zg.exe -help
.\zg.exe -install
```

`-install` registra la cartella nel PATH utente: non copia i file e non crea un'installazione MSI. Per il PATH di sistema apri un terminale come amministratore ed esegui `.\zg.exe -install --system-wide`. Riapri completamente il terminale dopo la registrazione.

Prima di spostare o cancellare la cartella, rimuovi la registrazione con lo stesso ambito:

```powershell
.\zg.exe -uninstall
# Solo per la registrazione di sistema, da terminale amministrativo:
.\zg.exe -uninstall --system-wide
```

Successivamente puoi eliminare la cartella estratta. `-uninstall` rimuove il riferimento dal PATH, non i file.

## Linux: installazione di sistema x64

Usa il formato della tua distribuzione. Non installare DEB e RPM nello stesso filesystem.

```sh
# Debian / Ubuntu:
sudo apt install ./zg-console_VERSION-1_amd64.deb
# Fedora / sistemi RPM compatibili:
sudo dnf install ./zg-console-VERSION-1.x86_64.rpm
# Arch Linux:
sudo pacman -U ./zg-console-VERSION-1-x86_64.pkg.tar.zst
```

I pacchetti installano in `/opt/zipgenius/zgshell` e gestiscono il collegamento `/usr/bin/zg`. Gli script del pacchetto curano la registrazione: non serve eseguire manualmente `-install`. Verifica con `zg -help`.

Il payload richiede glibc >= 2.34, ICU, OpenSSL 3, libgcc, libstdc++ e zlib. Non è un pacchetto per Alpine/musl. La conversione Linux verso 7Z e TAR.XZ richiede anche `libarchive.so.13` e il locale `C.UTF-8`. Il gestore risolve le dipendenze dichiarate usando i repository configurati.

Per aggiornare, installa il nuovo file con lo stesso comando. Per rimuovere, usa il gestore corrispondente:

```sh
sudo apt remove zg-console
sudo dnf remove zg-console
sudo pacman -R zg-console
```

## Linux e macOS: copia portabile

Estrai tutto il tar.gz in una cartella permanente. Esempio di installazione personale Linux x64:

```sh
mkdir -p "$HOME/.local/opt"
tar -xzf zg-console-VERSION-linux-x64.tar.gz -C "$HOME/.local/opt"
cd "$HOME/.local/opt/zg-console-VERSION-linux-x64"
chmod +x zg
./zg -help
./zg -install
```

Su macOS usa l'archivio `osx-x64` o `osx-arm64` e il nome corrispondente della cartella estratta. La registrazione personale aggiunge il percorso in `~/.bashrc` su Linux e `~/.zprofile` su macOS. Apri una nuova sessione Bash su Linux o una nuova sessione Zsh di login su macOS. Per altre shell aggiungi il percorso al PATH nella loro configurazione.

Per un'installazione di sistema, colloca prima l'intera distribuzione in una cartella permanente gestita dall'amministratore. Da quella cartella esegui `sudo ./zg -install --system-wide`. Viene scritto `/etc/profile.d/zipgenius-x.sh` su Linux o `/etc/paths.d/zipgenius-x` su macOS. Apri una nuova sessione di login. Non registrare nel PATH di sistema una cartella modificabile da utenti ordinari.

Prima di spostare o rimuovere la cartella, esegui `./zg -uninstall` oppure `sudo ./zg -uninstall --system-wide`, coerentemente con l'ambito usato. Poi puoi eliminare i file. Per aggiornare una copia portabile, rimuovi la vecchia registrazione, estrai l'intera nuova distribuzione e registra la nuova cartella. Non sostituire soltanto l'eseguibile.

## Snap e Flatpak: Linux x64

Sono pacchetti locali, non pubblicati su Snap Store o Flathub.

```sh
sudo snap install --dangerous ./zg-console_VERSION_amd64.snap
snap run zg-console -help
# Facoltativo, per supporti rimovibili:
sudo snap connect zg-console:removable-media
```

`--dangerous` consente l'installazione locale senza firma dello Snap Store; non disattiva i controlli ZIP. Snap usa core26 e isolamento strict: file nascosti e cartelle di sistema non sono generalmente accessibili. Il comando diretto è `zg-console`; per chiamarlo `zg` puoi scegliere esplicitamente `sudo snap alias zg-console zg`. Rimuovi con `sudo snap remove zg-console`.

```sh
flatpak --user remote-add --if-not-exists flathub https://dl.flathub.org/repo/flathub.flatpakrepo
flatpak --user install ./zg-console-VERSION-x86_64.flatpak
flatpak run it.zipgenius.Console -help
```

Serve il runtime Freedesktop Platform 25.08, ottenuto separatamente tramite Flatpak. Usa percorsi assoluti, ad esempio `flatpak run it.zipgenius.Console -info "$HOME/Downloads/disco.iso"`. Il pacchetto richiede accesso al filesystem host entro i permessi dell'utente e del sandbox, non accesso generale ai dispositivi. Rimuovi con `flatpak --user uninstall it.zipgenius.Console`.

Snap e Flatpak non usano la registrazione PATH dell'host: `-install` e `-uninstall` restituiscono `7`. Gestiscili con il loro package manager. [Dettagli sandbox](https://github.com/zipgenius/zgx_console/blob/main/zgx/packaging/SANDBOX-PACKAGES.md).

## Verificare quale copia viene eseguita

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

Se parte un'altra versione, controlla alias, cartella corrente e ordine del PATH. Una voce nel PATH utente Windows non prevale necessariamente su una copia nel PATH macchina. Quando necessario, avvia l'eseguibile desiderato con il percorso completo.
