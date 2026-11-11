# Arch Linux â€” zg-console

Pacchetto x86_64 in formato `.pkg.tar.zst`, generato dal publish Linux self-contained. Include .NET, mantiene il comando `zg` e la protezione ZipBomb sempre attiva. Non Ã¨ stato pubblicato in AUR nÃ© nei repository ufficiali Arch.

## Installazione

```sh
sudo pacman -U ./zg-console-10.0.9796.41133-1-x86_64.pkg.tar.zst
zg -help
zg -info "$HOME/Downloads/disco.iso" --verbose
```

Dipendenze: `glibc>=2.34`, `gcc-libs`, `icu`, `openssl`, `zlib`. Pacman ne verifica la presenza e puÃ² installarle dai repository configurati. Il pacchetto Ã¨ locale e non firmato. Gli aggiornamenti vengono installati con lo stesso comando `pacman -U` sul nuovo file; non Ã¨ stato configurato un repository di aggiornamento.

```sh
sudo pacman -R zg-console
```

L'app viene installata in `/opt/zipgenius/zgshell`; il pacchetto possiede il collegamento `/usr/bin/zg` e i metadati AppStream. Gli script post_install/post_upgrade eseguono `-install --system-wide --silent`; pre_remove esegue `-uninstall --system-wide --silent` prima della rimozione del binario. Gli altri contenuti del profilo PATH sono preservati. La licenza dichiarata resta GPL-3.0-only, come negli altri formati Linux.

## Generazione

Su Arch servono `base-devel`, `pacman`, Python 3 e un publish Linux x64 giÃ  prodotto. Su WSL Ubuntu sono disponibili i pacchetti `makepkg` e `pacman-package-manager`; non si compilano binari Arch su Ubuntu, si impacchettano i binari .NET self-contained giÃ  pubblicati. Il controllo delle dipendenze avviene sul sistema destinatario. Le informazioni di build indicano il sistema di assemblaggio e non pretendono che il publish sia stato compilato su Arch.

Dalla cartella ZipGenius X:

```powershell
& ../zgshell/packaging/Build-ArchPackage.ps1 -Distribution Ubuntu-26.04
# Include il formato Arch in una nuova build completa:
& ../zgshell/packaging/Build-Packages.ps1 -Distribution Ubuntu-26.04 -IncludeArchPackage
# Anche Snap e Flatpak:
& ../zgshell/packaging/Build-Packages.ps1 -Distribution Ubuntu-26.04 -IncludeArchPackage -IncludeSandboxPackages
```

`build-arch.py` accetta `--payload`, `--output`, `--version` e `--release`. Lo script PowerShell legge la versione dall'assembly del publish. Il template `arch/PKGBUILD` usa makepkg, include gli script di installazione, preserva gli assembly e gli stub disabilitando strip/debug/LTO e genera i metadati standard PKGINFO, BUILDINFO e MTREE. I file PDB non vengono distribuiti. SHA256SUMS comprende tutti i formati presenti.

## Verifica isolata

`verify-arch.py` richiede root e una directory Arch **dedicata ai test**, con le dipendenze giÃ  installate. Usa bubblewrap: il root del sistema principale e il suo database pacman non vengono modificati.

```sh
sudo python3 verify-arch.py --root /percorso/root-arch-di-prova --package /percorso/zg-console-10.0.9796.41133-1-x86_64.pkg.tar.zst --version 10.0.9796.41133
```

La verifica copre installazione e reinstallazione senza `--nodeps`, versione, symlink, help, aggiunta, test, informazioni, estrazione, creazione SFX, registrazione PATH e rimozione con ripristino delle righe preesistenti del profilo. Il root deve essere vuoto di precedenti installazioni zg-console. Non usare un root che contiene dati da conservare.

Riferimenti: [PKGBUILD](https://wiki.archlinux.org/title/PKGBUILD), [script di installazione ALPM](https://man.archlinux.org/man/alpm-install-scriptlet.5).

## Risultato della versione 10.0.9796.41133-1

Generato `zg-console-10.0.9796.41133-1-x86_64.pkg.tar.zst`; checksum verificato insieme a tutti gli altri formati. Makepkg ha prodotto PKGINFO, BUILDINFO, MTREE e script di installazione.

La prova è stata eseguita in un root Arch ufficiale isolato tramite bubblewrap, dopo verifica SHA-256 dell'archivio bootstrap e aggiornamento completo con pacman. Dipendenze Arch effettive risolte senza --nodeps. Superati installazione, reinstallazione, startup/versione, help, aggiunta, test, informazioni, estrazione, creazione SFX, rimozione e confronto SHA-256 del profilo PATH preesistente. Il pacchetto di prova è stato rimosso dal root. Il root del sistema WSL principale e il suo database dei pacchetti non sono stati usati per l'installazione.

Questo verifica l'ambiente utente Arch e le transazioni pacman, non un desktop Arch avviato, altre architetture o una revisione AUR. Log ignorati: packaging-arch-build.log, packaging-arch-environment.log e packaging-arch-verification.log, nella cartella zgshell.
