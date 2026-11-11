# Windows — installer unico

Distribuire `zg-console-10.0.9796.41133-win-x64-setup.exe`. È un programma nativo x64 e non richiede .NET per avviarsi; il runtime della console è incluso nei pacchetti al suo interno.

## Installazione

Apri il setup normalmente, senza scegliere Esegui come amministratore. Per una nuova installazione propone:

- **Solo per me**, scelta predefinita: cartella `%LOCALAPPDATA%\Programs\ZipGenius X Console`, registrazione nel PATH utente, nessuna elevazione.
- **Per tutti gli utenti del computer**: cartella Program Files e PATH di sistema; l'autorizzazione amministrativa viene richiesta dopo aver premuto Installa. Il pulsante mostra lo scudo per questa scelta.

Dopo l'installazione chiudi e riapri completamente il terminale per usare `zg` con il nuovo ambiente. La normale precedenza Windows del PATH continua ad applicarsi se esistono altre copie di zg.

## Installazioni esistenti

Il setup riconosce gli UpgradeCode dei due MSI precedenti. Se trova una sola modalità già installata, conserva quella e disabilita il cambio di ambito. Se entrambe le modalità sono presenti, permette di scegliere quale gestire, mantenendo distinte le due installazioni.

Il pulsante principale propone Ripara per il pacchetto già installato oppure Aggiorna per un'altra revisione compatibile. Disinstalla rimuove soltanto l'ambito selezionato. Una richiesta di cambiare ambito mentre è presente soltanto l'altro viene fermata prima dell'installazione: per cambiare modalità bisogna prima rimuovere la copia precedente. Una versione del setup più vecchia della versione installata non la sovrascrive.

I due MSI restano i motori interni, con le identità già usate dalle distribuzioni precedenti. Le applicazioni installate continuano a comparire in Impostazioni di Windows e possono essere rimosse da lì. Il setup unico non aggiunge una seconda voce autonoma di disinstallazione.

I file di lavoro e i log sono sotto `%TEMP%\ZipGeniusConsoleSetup`. Il pacchetto selezionato viene conservato per supportare la manutenzione, e rimosso dal setup dopo una disinstallazione riuscita. La pulizia dei file temporanei può rimuovere questa copia: in tal caso riaprire il setup EXE per riparare la console. Non occorre recuperare manualmente un MSI.

## Automazione

```powershell
# Installazione personale predefinita; conserva l'ambito se già installato:
.\zg-console-10.0.9796.41133-win-x64-setup.exe /quiet
# Installazione esplicitamente personale:
.\zg-console-10.0.9796.41133-win-x64-setup.exe /quiet /currentuser
# Installazione di sistema, con elevazione quando necessaria:
.\zg-console-10.0.9796.41133-win-x64-setup.exe /quiet /allusers
# Riparazione e rimozione nell'ambito rilevato:
.\zg-console-10.0.9796.41133-win-x64-setup.exe /quiet /repair
.\zg-console-10.0.9796.41133-win-x64-setup.exe /quiet /uninstall
```

Se entrambe le modalità sono installate, specificare /currentuser oppure /allusers. /quiet nasconde l'interfaccia del setup e di MSI, ma non sopprime la richiesta Windows di autorizzazione amministrativa. Per una distribuzione automatica di sistema completamente silenziosa avviare il processo da un contesto già autorizzato.

Codici principali: 0 successo, 3010 successo con riavvio necessario, 1223 autorizzazione annullata, 1605 nessuna installazione da rimuovere, 1618 altro setup già in corso, 1638 versione/ambito incompatibili. Gli altri errori Windows Installer vengono restituiti al chiamante. /plan file.json produce una diagnosi del percorso scelto, dell'ambito e della necessità di elevazione senza installare nulla; è usato dai test.

## Generazione

```powershell
# Dai due MSI già disponibili:
& ../zgshell/packaging/Build-WindowsSetup.ps1
# Nuovo publish Windows e setup unico (predefinito):
& ../zgshell/packaging/Build-Packages.ps1 -WindowsOnly
# Tutti i formati Linux opzionali e setup Windows unico:
& ../zgshell/packaging/Build-Packages.ps1 -Distribution Ubuntu-26.04 -IncludeArchPackage -IncludeSandboxPackages
```

Il valore predefinito WindowsScope è unified. I due MSI vengono generati in `work/windows-msi`, poi incorporati nel setup EXE; l'utente finale riceve un solo file. Machine/user/both restano disponibili come modalità avanzate per chi necessita di MSI separati. I vecchi file in dist restano artefatti storici: usare il nuovo -setup.exe per la distribuzione ordinaria.

Servono Visual Studio C++ x64 Build Tools e Windows SDK, oltre ai prerequisiti del packaging MSI. I sorgenti nativi sono in setup/. L'eseguibile dichiara asInvoker; soltanto il ramo di sistema usa runas. Il ramo utente avvia msiexec direttamente con il token del chiamante. I file temporanei ereditano i permessi della cartella temporanea Windows, senza cambiare le ACL del profilo o le policy del sistema. I pacchetti restano non firmati.

## Verifica

`Test-UnifiedSetup.ps1` richiede un terminale non elevato e rifiuta di sostituire un'installazione personale già presente. Prova il default utente, il piano di elevazione per la modalità macchina, installazione/riparazione/rimozione utente reali, rifiuto del cambio di ambito e aggiornamento da una copia di test dell'MSI precedente con un altro ProductCode. Verifica PATH utente e di sistema. La copia modificata resta sotto work ed è esclusa dalla distribuzione. Il test non pretende di coprire ogni versione precedente o ogni policy aziendale.

La verifica visiva controlla scelta predefinita, testi italiani e scudo della modalità di sistema. L'installazione di sistema effettiva richiede credenziali amministrative e deve essere distinta dal controllo del piano e dei metadati del vecchio MSI.

## Risultati della versione 10.0.9796.41133

Generato e verificato `zg-console-10.0.9796.41133-win-x64-setup.exe`. È un solo eseguibile nativo con i due MSI incorporati, circa 286 MiB. I checksum sono aggiornati per tutti i formati.

Prove reali con account Windows non elevato: installazione personale dal setup, riparazione, rimozione e aggiornamento da un MSI con l'identità precedente. Verificati default utente, mantenimento dell'ambito, rifiuto della conversione implicita utente/sistema, ripristino esatto del PATH utente e assenza di modifiche al PATH di sistema. Le installazioni di prova sono state rimosse. La schermata italiana e lo scudo della scelta di sistema sono stati controllati tramite Computer Use.

La modalità di sistema è stata verificata nel piano del launcher e nei metadati del relativo MSI; non è stata eseguita una transazione elevata né inserita alcuna credenziale amministrativa. Questo limite resta distinto dal collaudo completo della modalità personale.

I log MSI del launcher sono nella cartella temporanea standard Windows, non nella cartella applicativa sotto LocalAppData: le prove hanno rilevato problemi di accesso del servizio Installer a quest'ultima in questo ambiente. Il launcher utente usa CreateProcess con il token del chiamante; quello di sistema usa esplicitamente runas. Log del lavoro ignorati sotto zgshell: packaging-unified-build.log e packaging-unified-verification.log; copie diagnostiche dei piani e dell'MSI di migrazione in packaging/work/unified-setup.
