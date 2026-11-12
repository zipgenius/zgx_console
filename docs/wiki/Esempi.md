# Esempi d'uso

[Home](Home) · [Installazione](Installazione) · [English](Usage)

## Sintassi e guida incorporata

```text
zg -comando archivio-o-file parametri opzioni-globali
```

Gli esempi presuppongono che `zg` sia nel PATH e che la cartella corrente contenga `documents` con file di prova. In alternativa usa il percorso completo, `./zg` su Unix o `.\zg.exe` in PowerShell. Salva gli archivi fuori dalla cartella sorgente. Usa virgolette dritte, come negli esempi.

```sh
zg -help
zg -help -add
zg -help -extract
zg -help -test
zg -help -exitcodes
```

## Creare e aggiornare uno ZIP

```sh
zg -add documents.zip "+documents/*" R1 F1 C5
zg -add documents.zip "+notes.txt" C5
```

Il primo comando include le sottocartelle (`R1`), conserva i percorsi relativi (`F1`) e applica compressione media (`C5`). Il secondo aggiunge un file allo stesso archivio. Racchiudi l'intero argomento `+modello` tra virgolette per evitare l'espansione dei caratteri jolly da parte della shell.

`R0` esclude le sottocartelle, `F0` elimina i percorsi, `C0` memorizza senza comprimere, `C9` seleziona la compressione massima. Il valore predefinito di `-add` è `C0`: indica esplicitamente il livello se vuoi comprimere.

Escludere i file temporanei e creare uno ZIP protetto AES-256:

```sh
zg -add clean.zip "+documents/*" "-*.tmp" R1 F1 C5
zg -add protected.zip "+documents/*" R1 F1 C5 PExampleOnly-ChangeMe Q3
```

Sostituisci la password di esempio. Le password sulla riga di comando possono restare nella cronologia o comparire negli argomenti del processo. Per estrazione e test interattivi preferisci la richiesta con digitazione nascosta. `Q3` seleziona AES-256; il solo `Ppassword` usa invece ZipCrypto tradizionale. Consulta `zg -help -add` per selezioni e liste `@list.txt`. Usa `AM` solo se vuoi spostare i file: elimina gli originali dopo la compressione.

## Elencare, verificare ed estrarre

```sh
zg -list documents.zip --verbose
zg -test documents.zip
zg -extract documents.zip extracted R1 F1 O1
```

`-list` legge i metadati, senza verificare i dati dei file. `-test` legge il contenuto senza salvare file estratti. `-extract` richiede la destinazione: `O1` salta i file esistenti, `O0` li sovrascrive sempre, `O2` chiede conferma ed è il valore predefinito. `F1` ricostruisce i percorsi, `R1` include le sottocartelle. `N1` crea un'ulteriore cartella con il nome dell'archivio; la destinazione `-curr` indica la cartella corrente.

Estrarre soltanto i file di testo, escludendo le bozze:

```sh
zg -extract documents.zip selected R1 F1 O1 "+*.txt" "-draft*"
```

Per un archivio protetto, ometti la password per ricevere la richiesta interattiva:

```sh
zg -test protected.zip
zg -extract protected.zip extracted-protected O1
```

Con `--silent` o input reindirizzato non viene chiesta la password: devi fornirla esplicitamente.

## Backup ZIP

```sh
zg -backup backup.zip documents T C5
zg -backup rolling.zip documents C5
zg -backup rolling.zip documents U C5
```

`T` aggiunge `_YYYYMMDD_HHmm` al nome; le sottocartelle sono incluse per impostazione predefinita. Senza `U` un nome già esistente viene rifiutato. `U` aggiorna il backup, ma non va considerato una sincronizzazione con eliminazione delle voci obsolete. Tieni il backup fuori dalla sorgente. `--documents`, `--pictures` e le opzioni analoghe selezionano le cartelle personali.

## Hash e informazioni sulle immagini disco

```sh
zg -hash documents.zip --sha256
zg -hash documents.zip --sha256 --sha512 --silent
zg -info installation.iso --verbose
zg -info machine.vhdx --verbose --silent > disk-report.txt
```

Con `-hash`, `--silent` stampa soltanto gli hash; con `-info` conserva il rapporto. La lettura delle immagini è in sola lettura e non le monta. Mantieni immagini padre e segmenti nelle posizioni previste. Il codice `9` indica un rapporto incompleto. I checksum memorizzati mostrati dai metadati non sostituiscono `-test`.

## Convertire un archivio

```sh
zg -convert documents.zip documents.tar.gz C5
```

La conversione estrae in una cartella temporanea privata e ricrea il risultato: serve spazio sufficiente. La sorgente rimane invariata. Una destinazione già esistente viene rifiutata salvo uso di `--overwrite`. Il supporto varia per piattaforma: su Linux la scrittura 7Z/TAR.XZ richiede libarchive; SQX e creazione CAB richiedono componenti Windows. Consulta i [limiti](Platform-support#italiano).

La build Windows controllata per questa guida ha restituito `99` convertendo in 7Z uno ZIP con voci di cartella. Per questo caso usa l'esempio TAR.GZ sopra; la conversione 7Z su Windows non viene dichiarata verificata qui.

## Creare un archivio autoestraente

```sh
zg -sfx --list-targets
```

Scegli un modello effettivamente incluso nella tua copia. Esempio su Windows x64:

```powershell
zg -sfx documents.zip documents.exe --ui=terminal --target=win-x64
```

Esempio su Linux x64:

```sh
zg -sfx documents.zip documents.run --ui=terminal --target=linux-x64
```

La creazione non avvia il risultato. Al destinatario non servono ZipGenius o .NET. La modalità grafica richiede una sessione desktop; creazione e firma locale dei pacchetti macOS richiedono un Mac. Un modello mancante viene segnalato, senza sostituirlo con un'altra piattaforma. `zg -help -sfx` descrive le altre opzioni.

## Script e codici di uscita

PowerShell:

```powershell
zg -test documents.zip --silent
$result = $LASTEXITCODE
if ($result -eq 0) { "Archivio verificato" } else { "Verifica non riuscita: $result" }
```

Bash/Zsh:

```sh
zg -test documents.zip --silent
result=$?
if [ "$result" -eq 0 ]; then echo "Archivio verificato"; else echo "Verifica non riuscita: $result"; fi
```

Leggi subito il codice. Risultati principali di `-test`:

| Codice | Significato |
| --- | --- |
| 0 | Controlli disponibili superati |
| 3 | Archivio mancante |
| 6 | Dati o struttura danneggiati/illeggibili |
| 7 | Formato o operazione non supportati |
| 8 | Dati protetti non verificati |
| 20 | Annullamento |
| 30 | Errore di accesso o del motore |
| 42 | Rischio ZIP alto: operazione bloccata |
| 43 | Controllo di sicurezza ZIP incompleto: operazione bloccata |
| 99 | Argomenti non validi |

`-list` può stampare l'elenco e restituire `41` per un avviso, oppure `42`/`43` per rischio alto/controllo incompleto. Per aggiunta, estrazione, backup, conversione e test un avviso moderato consente di continuare; se l'operazione riesce, restituisce `0`.

`--silent` nasconde l'output ordinario, `--noprogress` nasconde l'avanzamento. `--yes` non supera i blocchi di sicurezza. Consulta l'aiuto del singolo comando per interpretare gli altri codici.
