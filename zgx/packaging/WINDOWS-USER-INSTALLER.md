# Windows — MSI personale (modalità avanzata)

Per la distribuzione ordinaria usare il nuovo [setup unico](WINDOWS-UNIFIED-SETUP.md), che propone la scelta utente/sistema. L'MSI personale separato rimane disponibile per installazioni avanzate.

Il pacchetto `zg-console-10.0.9796.41133-win-x64-user.msi` installa ZipGenius X Console senza richiedere privilegi amministrativi. Include il runtime .NET, come la variante per tutti gli utenti.

## Uso

Apri l'MSI con doppio clic da una normale sessione Windows. L'installazione va in:

```text
%LOCALAPPDATA%\Programs\ZipGenius X Console
```

Non è necessario scegliere l'esecuzione come amministratore. L'app compare fra le applicazioni installate dell'utente come **ZipGenius X Console (Current user)** e può essere rimossa da Impostazioni di Windows. Per installazione/rimozione silenziosa:

```powershell
msiexec /i zg-console-10.0.9796.41133-win-x64-user.msi /qn
msiexec /x zg-console-10.0.9796.41133-win-x64-user.msi /qn
```

Dopo il setup chiudi e riapri completamente l'app terminale per usare `zg` nel nuovo ambiente. L'installer esegue `zg -install --silent`; alla rimozione esegue `zg -uninstall --silent`. Non usa `--system-wide` e non cambia il PATH di sistema. Le altre voci del PATH utente sono preservate.

L'installer per tutti gli utenti resta `zg-console-10.0.9796.41133-win-x64.msi`, installa in Program Files e richiede privilegi amministrativi. Le due varianti hanno UpgradeCode differenti: una non converte né disinstalla automaticamente l'altra. Se nel PATH di sistema esiste già un altro `zg.exe`, la normale precedenza di Windows può continuare a scegliere quello; l'installer utente non modifica le registrazioni di sistema. In quel caso si può usare il percorso esplicito della copia personale.

## Implementazione

Il nuovo MSI dichiara Scope=perUser (privilegi limitati), usa LocalAppDataFolder e scrive i propri riferimenti soltanto in HKCU. Le azioni differite e quelle di rollback impersonano l'utente chiamante; i risultati sono controllati. La vecchia versione dello stesso installer utente viene rimossa prima di copiare la nuova. La riparazione riesegue la registrazione in modo idempotente.

I componenti sotto il profilo hanno riferimenti HKCU stabili, identificativi basati sui percorsi relativi e rimozione delle cartelle vuote. Il formato mantiene la versione dell'assembly pubblicato. MSI confronta solo i primi tre campi e consente aggiornamenti fra revisioni della stessa giornata, come l'installer di sistema; ciò consente anche un ritorno a una revisione precedente dello stesso giorno.

## Rigenerazione

Dalla cartella ZipGenius X:

```powershell
# Usa il publish Windows già disponibile:
& ../zgx/packaging/Build-Packages.ps1 -WindowsOnly -SkipPublish -WindowsScope user
# Nuovo publish e MSI separati:
& ../zgx/packaging/Build-Packages.ps1 -WindowsOnly -WindowsScope both
# Scegli solo l'installer di sistema:
& ../zgx/packaging/Build-Packages.ps1 -WindowsOnly -WindowsScope machine
```

WindowsScope accetta unified, machine, user o both; il valore predefinito è unified. La scelta non cambia i formati Linux. Le due varianti Windows condividono il payload pubblicato, ma non l'identità MSI. SHA256SUMS include entrambi i file.

## Verifica

```powershell
& ../zgx/packaging/Verify-Msi.ps1 -UserScope
& ../zgx/packaging/Test-UserInstaller.ps1
```

Il primo script controlla identità delle azioni, privilegi limitati, assenza di ALLUSERS, scritture HKCU, estrazione e avvio. Il secondo deve essere eseguito senza elevazione e rifiuta di sostituire un'installazione personale già presente. Installa e ripara il prodotto, prova un aggiornamento MSI reale tramite una copia di test con ProductCode/PackageCode nuovi e lo stesso payload/versione, quindi disinstalla. La copia di prova resta in work ed è esclusa dalla distribuzione. Sono verificati file, PATH utente, PATH di sistema e rimozione della cartella personale. Non viene dichiarato il collaudo di aggiornamenti tra tutte le versioni future dell'applicazione.

## Risultati della versione 10.0.9796.41133

Generato l'MSI utente aggiuntivo e verificati i metadati del database: privilegi limitati, nessun ALLUSERS, registro soltanto HKCU e azioni impersonate. Estrazione e avvio dell'app riusciti.

Sono state eseguite transazioni MSI reali con token Windows non elevato: installazione, riparazione, major upgrade tramite la copia di prova e rimozione. Tutte concluse con codice 0; verificati il ripristino esatto del PATH utente, l'assenza di modifiche al PATH di sistema e la rimozione della cartella installata. Non è stato necessario fornire credenziali amministrative. L'installazione di prova è stata rimossa. Non è un collaudo delle policy di distribuzione di ogni ambiente aziendale.

Log ignorati sotto zgx: packaging-user-build.log, packaging-user-msi-verification.log, packaging-user-lifecycle.log; dettagli delle transazioni in packaging/work/user-*.log. La versione resta allineata al publish corrente e agli altri pacchetti; non è stata ricompilata l'app per il solo cambio di ambito MSI.
