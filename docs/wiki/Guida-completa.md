# ZipGenius X console

Versione 1.0 - 12 novembre 2026

[Italiano](Guida-completa) | [English](Command-reference) | [PDF](https://github.com/zipgenius/zgx_console/wiki/downloads/ZipGenius-X-console-it.pdf)

<a name="uso"></a>

## Uso generale

`zg -comando file {parametri} [opzioni globali]`

In PowerShell, se il percorso dell'eseguibile contiene spazi, usare l'operatore `&`:

```
& "C:\percorso\zg.exe" -list "C:\Archivi\documenti.zip"
```

I parametri di inclusione di `-add` devono mantenere il segno `+` dentro la stessa stringa del percorso, per esempio `"+C:\Foto\*.*"`.

<a name="riepilogo"></a>

## Tabella delle sintassi

| Comando | Sintassi breve | Funzione |
| --- | --- | --- |
| [`-add`](#add) | `zg -add archivio +[file] [parametri]` | Crea o aggiorna un archivio. |
| [`-list`](#list) | `zg -list archivio` | Elenca il contenuto. |
| [`-extract`](#extract) | `zg -extract archivio destinazione` | Estrae un archivio. |
| [`-hash`](#hash) | `zg -hash file --algoritmo` | Calcola uno o più hash. |
| [`-delete`](#delete) | `zg -delete archivio.zip +[file]` | Elimina voci da un file ZIP. |
| [`-destroy`](#destroy) | `zg -destroy file [passaggi]` | Sovrascrive il contenuto di un file. |
| [`-dumpdisc`](#dumpdisc) | `zg -dumpdisc immagine [opzioni]` | Mostra il contenuto di ISO/VHD/VHDX. |
| [`-hexview`](#hexview) | `zg -hexview file [parametri]` | Visualizza i byte in formato esadecimale. |
| [`-install`](#install) | `zg -install [--system-wide]` | Installa l'applicazione. |
| [`-uninstall`](#install) | `zg -uninstall [--system-wide]` | Disinstalla l'applicazione. |
| [`-help`](#help) | `zg -help [-comando]` | Mostra l'aiuto integrato. |
| [`-test`](#test) | `zg -test archivio [Ppassword]` | Controlla l’integrità senza estrarre file. |
| [`-backup`](#backup) | `zg -backup archivio sorgente [opzioni]` | Crea un backup ZIP. |
| [`-convert`](#convert) | `zg -convert sorgente destinazione [opzioni]` | Converte un archivio in un altro formato. |
| [`-sfx`](#sfx) | `zg -sfx archivio destinazione [opzioni]` | Crea un pacchetto autoestraente, con finestra o terminale. |
| [`-info`](#info) | `zg -info file [--verbose]` | Mostra informazioni su file, archivi e immagini disco. |

<a name="opzioni"></a>

## Opzioni globali

| Opzione | Effetto |
| --- | --- |
| `--silent` | Nasconde i messaggi ordinari, l’intestazione e l’avanzamento. Gli avvisi di sicurezza restano visibili. |
| `--verbose` | Mostra informazioni aggiuntive. |
| `--yes` | Conferma automaticamente dove supportato. |
| `--noprogress` | Nasconde le barre di avanzamento. |
| `--wait` | Attende un tasto prima di terminare. |

<a name="zip-security"></a>

## Protezione dagli archivi pericolosi

Uno ZIP può essere molto piccolo ma richiedere uno spazio enorme quando viene aperto: questo tipo di archivio viene chiamato "ZipBomb" e può riempire il disco o bloccare il computer.

zg controlla gli ZIP automaticamente. La protezione è sempre attiva: non occorre impostarla e non può essere disattivata. Anche --silent e --yes lasciano attivi i controlli e gli avvisi di sicurezza.

Un avviso indica un sospetto: l'operazione può continuare. Un rischio alto blocca l'operazione. Se il controllo non può essere completato, zg si ferma per prudenza; questo non significa necessariamente che il file sia pericoloso.

Se compare un blocco, evita di riprovare alla cieca: chiedi una nuova copia del file a chi te lo ha fornito o usa una copia attendibile. Alcuni ZIP protetti che contengono altri ZIP possono non essere controllabili.

Il controllo riguarda gli ZIP e i file basati sul formato ZIP, anche se rinominati. Non è una verifica antivirus e un punteggio basso non garantisce che qualsiasi contenuto sia innocuo.

### Codici per chi usa script o operazioni automatiche

Con -list, il codice finale permette di decidere cosa fare senza leggere il messaggio:

0: nessun rischio significativo rilevato (score sotto 40), se l'elenco è stato letto correttamente.

41: avviso, score da 40 a 59.

42: rischio alto, score da 60 a 99.

43: controllo incompleto, salvo un rischio alto già rilevato.

I normali errori, come un file inesistente, mantengono i propri codici.

Con -add, -extract, -backup, -convert, -sfx e -test, 42 e 43 indicano un blocco di sicurezza. Un semplice avviso lascia proseguire: se l'operazione riesce, il codice finale è 0.

L'elenco e gli avvisi usano due uscite separate (stdout e stderr). Salvare l'elenco in un file non nasconde gli avvisi. Nei messaggi, risk-code descrive il rischio; non è sempre il codice finale dell'operazione.

<a name="add"></a>

## `-add` alias -a

`zg -add archivio {C...} {A...} {F...} {R...} {K...} +[file] -[file]`

Crea o aggiorna ZIP, 7Z, TAR e relativi flussi compressi, CAB e SQX. Sono supportati, tra gli altri, `.tar.gz`/`.tgz`, `.tar.bz2`/`.tbz2` e `.tar.Z`/`.taz`. I formati `.bz2` e `.Z` contengono un solo file.

| Parametro | Significato |
| --- | --- |
| `+[file]` | Include un file, una cartella o una maschera. Ripetibile. |
| `-[file]` | Esclude un file, una cartella o una maschera. Ripetibile. |
| `@lista.txt` | Legge inclusioni ed esclusioni da un file di testo. Ogni riga deve usare la sintassi `+[file]` per includere oppure `-[file]` per escludere un percorso o una maschera. |
| `C0 C1 C5 C9 CB` | Livello di compressione: nessuno, rapido, normale, massimo o brutal. |
| `AA AM AU` | Aggiungi, sposta eliminando gli originali, oppure aggiorna/sostituisci. |
| `F0 F1 F2 F3` | Nessun percorso, relativo, completo senza unità, completo con unità. |
| `R0 R1` | Disattiva o attiva la scansione ricorsiva. |
| `K0 K1` | Nasconde o mostra la barra di avanzamento. |
| `Ppassword` | Imposta la password nei formati compatibili. |
| `Q1 Q2 Q3` | ZIP Crypto, AES-128 o AES-256. |
| `Vvalore` | Dimensione dei volumi nei formati compatibili. |

### Esempi

```
zg -add "C:\Archivi\foto.zip" C9 AU R1 F1 K1 "+C:\Foto\*.*"
zg -add "C:\Archivi\foto.tar.gz" C5 AU R1 F1 K1 "+C:\Foto\*.*"
zg -add "C:\Archivi\foto.tar.bz2" C5 AU R1 F1 K1 "+C:\Foto\*.*"
zg -add "C:\Archivi\documenti.7z" C9 AA R1 F1 K1 "+C:\Documenti\*.pdf" "-bozza*"
zg -add "C:\Archivi\singolo.bz2" C5 AU F0 K1 "+C:\Dati\report.csv"
```

### Protezione ZIP

Prima di creare o aggiornare l'archivio, zg controlla lo ZIP già esistente e gli ZIP scelti da aggiungere, anche nelle sottocartelle incluse. Con un avviso può continuare; con rischio alto o controllo incompleto si ferma prima di modificare l'archivio o spostare i file originali.

[Avvisi, blocchi e codici per gli script](#zip-security)

Per proteggere uno ZIP con una password usa `-add` con `Ppassword` e `Q3` (AES-256). Esempio: `zg -add documenti.zip Psegreta Q3 +documento.txt`. Sostituisci `segreta` con la tua password.

<a name="list"></a>

## `-list` alias -l

`zg -list archivio [--verbose]`

Elenca i file contenuti nell'archivio. Con `--verbose` aggiunge dimensioni, date, rapporto di compressione e CRC quando disponibili.

### Esempi

```
zg -list "C:\Archivi\foto.zip"
zg -list "C:\Archivi\backup.7z" --verbose
```

### Protezione ZIP

Mostra i nomi dei file senza estrarli sul disco e segnala eventuali sospetti. Puoi quindi consultare l'elenco anche quando l'estrazione sarebbe bloccata, se l'elenco è leggibile. Il punteggio (score) indica il livello di rischio stimato: non è una certezza né una probabilità esatta.

[Avvisi, blocchi e codici per gli script](#zip-security)

<a name="extract"></a>

## `-extract` alias -x

`zg -extract archivio destinazione [opzioni]`

Questo comando estrae uno o più file dall'archivio specificato. `archivio` indica il file da decomprimere; `destinazione` indica la cartella nella quale salvare i file estratti. Entrambi i parametri sono obbligatori. Per estrarre nella cartella corrente usare `./` oppure `-curr`. Se non viene indicato alcun `+[file]`, vengono considerati tutti i file ammessi dalle altre opzioni.

### Opzioni

|  |  |
| --- | --- |
| `R0 R1` | Estrae solo i file nella radice oppure include anche quelli nelle sottocartelle dell'archivio. Predefinito: R1. |
| `F0 F1` | Ignora i percorsi memorizzati oppure ricrea nella destinazione la struttura originale delle cartelle. Predefinito: F1. |
| `N0 N1` | Estrae nella destinazione oppure crea al suo interno una sottocartella con il nome dell'archivio. Predefinito: N0. |
| `O0 O1 O2` | In presenza di file già esistenti: li sovrascrive, li salta oppure chiede all'utente cosa fare. Predefinito: O2. |
| `Ppassword` | Specifica la password necessaria per decomprimere un archivio protetto. Se manca o non è corretta, nelle sessioni interattive viene richiesta fino a tre volte; `Esc` annulla l'operazione. Con `--silent` non viene mostrata alcuna richiesta. |
| `K0 K1` | Nasconde oppure mostra l'indicatore di avanzamento. Predefinito: K1. |
| `+[file]` | Include nell'estrazione il file, la cartella o la maschera specificata. Sono ammessi i caratteri jolly `*` e `?`; il parametro è ripetibile. |
| `-[file]` | Esclude dall'estrazione il file, la cartella o la maschera specificata. Sono ammessi i caratteri jolly `*` e `?`; il parametro è ripetibile. |
| `@lista.txt` | Legge inclusioni ed esclusioni da un file di testo contenente righe che iniziano con `+[file]` o `-[file]`. |
| `Dattributi` | Estrae soltanto i file con gli attributi indicati: A archivio, H nascosto, R sola lettura, S sistema, Z nessuno. X accetta qualsiasi attributo. |

### Destinazioni speciali

Usa uno dei seguenti parametri al posto del percorso di destinazione.

| Parametro | Destinazione |
| --- | --- |
| `[DOCS]` | Cartella Documenti dell'utente. |
| `[MUSIC]` | Cartella Musica dell'utente. |
| `[PICS]` | Cartella Immagini dell'utente. |
| `[VIDEO]` | Cartella Video dell'utente. |
| `[FAV]` | Cartella Preferiti dell'utente. |
| `[SYS]` | Cartella di sistema di Windows. |
| `[WIN]` | Cartella di installazione di Windows. |
| `[TEMP]` | Cartella dei file temporanei. |
| `[CURR]` | Cartella di lavoro corrente. |
| `[DESKTOP]` | Cartella Desktop dell'utente. |

### Esempi

```
zg -extract "C:\Archivi\foto.zip" "C:\Estratti\Foto" R1 F1 O0
zg -extract "C:\Archivi\protetto.7z" "D:\Ripristino" R1 F1 O0 --noprogress
zg -extract "C:\Archivi\backup.7z" -curr R1 Psegreta +"*.txt" --silent
```

### Protezione ZIP

Prima di estrarre, zg controlla lo ZIP. Un avviso consente di continuare; un rischio alto o un controllo incompleto bloccano l'estrazione prima di creare la cartella di destinazione. Il controllo riguarda l'intero ZIP, anche quando scegli di estrarre soltanto alcuni file.

[Avvisi, blocchi e codici per gli script](#zip-security)

<a name="hash"></a>

## `-hash` alias -hh

`zg -hash file --algoritmo [--altro-algoritmo ...]`

Durante il calcolo viene mostrato l'algoritmo corrente e aggiornato l'indicatore di attività. Con più algoritmi, ogni hash è seguito da una riga vuota.

### Parametri

| Parametro | Significato |
| --- | --- |
| `file` | Percorso del file di cui calcolare gli hash. Obbligatorio. |
| `--md5` | Calcola MD5. |
| `--sha1` | Calcola SHA-1. |
| `--sha224`, `--sha256`, `--sha384`, `--sha512` | Calcola la variante SHA-2 indicata. |
| `--sha3-224`, `--sha3-256`, `--sha3-512` | Calcola la variante SHA-3 indicata. |
| `--ripemd128`, `--ripemd160`, `--ripemd256`, `--ripemd320` | Calcola la variante RIPEMD indicata. |
| `--tiger` | Calcola Tiger. |
| `--whirlpool` | Calcola Whirlpool. |
| `--all` | Calcola tutti gli algoritmi disponibili. Sostituisce la selezione dei singoli algoritmi. |
| `--silent` | Mostra solo i valori hash, senza indicatore né etichette. Con più algoritmi mantiene le righe vuote. |
| `--noprogress` | Nasconde l'indicatore di attività durante il calcolo. |

Le opzioni dei singoli algoritmi possono essere ripetute nello stesso comando. Sono disponibili anche le altre [opzioni globali](#opzioni).

### Esempi

```
zg -hash "C:\Download\setup.exe" --sha256
zg -hash "C:\Download\setup.exe" --md5 --sha1 --sha3-256
zg -hash "C:\Download\setup.exe" --all
```

<a name="delete"></a>

## `-delete` alias -d

`zg -delete archivio.zip +[file] [...]`

Elimina una o più voci da un archivio ZIP. Usare `+[file]` per selezionare gli elementi e `-[file]` per escludere quelli corrispondenti; sono ammessi i caratteri jolly.

### Esempi

```
zg -delete "C:\Archivi\dati.zip" "+temporanei\*"
zg -delete "C:\Archivi\dati.zip" "+readme.old" "+cache\*" K1
```

<a name="destroy"></a>

## `-destroy`

`zg -destroy file [numero-passaggi] [--yes]`

Sovrascrive il contenuto del file con dati casuali. Il file rimane sul disco. Sono consentiti al massimo 10 passaggi. L'operazione non è annullabile.

### Esempi

```
zg -destroy "C:\Archivi\riservato.zip"
zg -destroy "C:\Archivi\riservato.zip" 3 --yes
```

<a name="dumpdisc"></a>

## `-dumpdisc` alias -dd

`zg -dumpdisc immagine.iso [--only-dirs|--only-files] [--show-size] [--show-summary] [--show-title] [--show-hash]`

Esamina immagini ISO, VHD e VHDX. Le opzioni dettagliate sono particolarmente utili per ISO.

### Parametri

| Parametro | Significato |
| --- | --- |
| `immagine.iso` | Percorso dell'immagine ISO, VHD o VHDX da esaminare. Obbligatorio. |
| `--only-dirs` (`-od`) | Mostra solo le cartelle dell'immagine ISO. |
| `--only-files` (`-of`) | Mostra solo i file dell'immagine ISO. Se usato insieme a `--only-dirs`, vengono mostrati entrambi. |
| `--show-size` (`-sz`) | Mostra le dimensioni dei file nell'elenco ISO. |
| `--show-summary` (`-sm`) | Mostra il numero totale di cartelle e file nell'immagine ISO. |
| `--show-title` (`-st`) | Mostra l'intestazione ISO con nome, dimensione e metadati del volume. |
| `--show-hash` (`-sh`) | Calcola e mostra lo SHA-256 dell'immagine ISO nell'intestazione; richiede `--show-title`. |
| `--show-date` (`-sd`) | Attualmente mostra le dimensioni dei file come `--show-size`. |

Le opzioni della tabella si applicano alle immagini ISO. Per VHD e VHDX viene mostrato il contenuto delle partizioni.

### Esempi

```
zg -dumpdisc "C:\Immagini\sistema.iso" --show-title --show-size --show-summary
zg -dumpdisc "C:\Immagini\sistema.iso" --only-files --show-hash
```

<a name="hexview"></a>

## `-hexview`

`zg -hexview file [P] [M] [D] [S=offset] [L=righe] [FB=byte-esadecimali|FT=testo] [BL=righe]`

### Opzioni

| Parametro | Significato |
| --- | --- |
| `file` | Percorso del file da visualizzare. Obbligatorio. |
| `P` | Attiva le pause ogni 20 righe, salvo diversa indicazione con `L=`. |
| `M` | Disattiva l'alternanza dei colori delle righe. |
| `D` | Mostra gli offset in formato decimale invece che esadecimale. |
| `S=offset` | Inizia la visualizzazione dall'offset indicato in byte. Valore predefinito: 0. |
| `L=righe` | Attiva le pause dopo il numero di righe indicato (da 1 a 500). Valore predefinito: 20. |
| `FB=byte-esadecimali` | Cerca una sequenza di byte esadecimali nel file, per esempio `FB=504B0304`. |
| `FT=testo` | Cerca testo codificato in UTF-8; la ricerca distingue maiuscole e minuscole. |
| `BL=righe` | Numero di righe mostrate per ogni risultato di `FB=` o `FT=` (da 1 a 10). Valore predefinito: 1. |

I parametri vanno scritti in maiuscolo, come negli esempi.

### Esempi

```
zg -hexview "C:\Dati\file.bin"
zg -hexview "C:\Dati\file.bin" M D S=1024 L=40
```

<a name="install"></a>

## `-install` e `-uninstall`

`zg -install [--system-wide|-s]`  
`zg -uninstall [--system-wide|-s]`

Aggiunge o rimuove la directory dell'applicazione dal PATH dell'utente corrente. Usa `--system-wide` o `-s` per applicare la modifica a tutti gli utenti; sono richiesti i privilegi di amministratore.

### Esempi

```
zg -install
zg -install --system-wide
zg -uninstall -s
```

<a name="help"></a>

## `-help` alias -?

`zg -help [-comando]`

Mostra l'aiuto generale o la pagina integrata di uno specifico comando.

### Esempi

```
zg -help
zg -help -add
zg -help -hash
zg -help -test
zg -help -exitcodes
```

<a name="backup"></a>

## `-backup`

`zg -backup archivio sorgente [opzioni]`

Il backup usa gli stessi controlli di -add: verifica il backup ZIP già esistente e gli ZIP scelti da salvare. Un avviso consente di continuare; rischio alto o controllo incompleto fermano il backup prima delle modifiche.

`zg -help -backup`: mostra tutte le opzioni del comando.

[Protezione dagli archivi pericolosi](#zip-security)

<a name="convert"></a>

## `-convert`

`zg -convert sorgente destinazione [opzioni]`

La conversione deve prima estrarre il contenuto. Se lo ZIP sorgente presenta un rischio alto o non può essere controllato completamente, zg si ferma senza sostituire il file di destinazione. Un semplice avviso consente di continuare. Anche gli ZIP estratti che vengono aggiunti al nuovo archivio sono controllati. L'archivio sorgente resta invariato.

`zg -help -convert`: mostra tutte le opzioni del comando.

[Protezione dagli archivi pericolosi](#zip-security)

## Comandi non riconosciuti

Il comando -split non è disponibile in zgx. Se lo usi, zg segnala un comando non riconosciuto e termina con codice 99.

Se il nome del comando non è riconosciuto, zg mostra un messaggio e termina con codice 99. Usa zg -help per consultare i comandi disponibili. Le opzioni globali con due trattini, come --p per nascondere l’avanzamento, restano disponibili.

<a name="sfx"></a>

## `-sfx`

`Sintassi:`

`zg -sfx archivio.zip destinazione [--ui=gui|terminal] [--target=piattaforma] [opzioni]`

Crea un pacchetto autoestraente che permette di estrarre i file senza installare ZipGenius X, .NET o 7-Zip sul computer del destinatario. Puoi partire da ZIP, 7Z o RAR. Il pacchetto deve corrispondere al sistema operativo e al tipo di processore di chi lo apre.

L'archivio originale resta invariato. Il comando crea il pacchetto, senza aprirlo né eseguire i file al suo interno.

### Opzioni

--ui=gui Finestra con pulsanti, scelta della cartella, password e avanzamento (impostazione iniziale). Richiede un ambiente desktop.

--ui=terminal Domande e avanzamento nella finestra del terminale. Adatto anche a Linux senza desktop.

--target=piattaforma win-x64, win-arm64, linux-x64, linux-arm64, osx-x64 oppure osx-arm64. Se manca, usa il sistema e il processore del computer che crea il pacchetto.

--message="testo" Messaggio mostrato a chi apre il pacchetto (da 1 a 319 caratteri).

--auto-start Inizia l'estrazione quando il destinatario apre il pacchetto. Normalmente attende il pulsante Estrai.

--file-list=hidden Nasconde l'elenco dei file (impostazione iniziale).

--file-list=expanded Mostra l'elenco dei file.

--file-list=collapsed Mostra l'elenco inizialmente chiuso, che si può aprire.

--no-readme Non apre automaticamente il file Leggimi dopo l'estrazione. Normalmente lo apre, se presente.

--keep-open Lascia aperta la finestra dopo l'estrazione. Normalmente si chiude al termine di un'estrazione riuscita.

--destination=cartella Sceglie la cartella di estrazione: editable, desktop, documents, downloads, pictures, music, videos o favorites.

Con editable (impostazione iniziale) il destinatario sceglie il percorso. Le altre cartelle sono quelle del suo computer e non possono essere cambiate nella finestra SFX.

--subfolder="percorso" Sottocartella nella destinazione scelta, per esempio Progetto/Documenti. Richiede una destinazione diversa da editable. Usa un percorso relativo, senza .., fino a 127 caratteri.

--run-after="file" Apre un file dell'archivio dopo l'estrazione, per esempio cartella/istruzioni.txt. Indica il percorso nell'archivio, non un comando con argomenti. Normalmente non viene aperto alcun file aggiuntivo.

--icon="file.ico" Usa un'icona personalizzata. Senza questa opzione usa l'icona standard.

--overwrite Sostituisce un .exe esistente soltanto dopo la creazione riuscita del nuovo pacchetto. Senza questa opzione un file esistente non viene sostituito.

La password di un archivio protetto non viene memorizzata nello SFX: viene richiesta al destinatario quando serve per estrarre. Per --run-after, l'elenco dell'archivio deve essere leggibile senza password.

### Protezione ZIP

La sorgente ZIP viene controllata prima di creare il pacchetto. Un avviso consente di continuare; rischio alto (codice 42) o controllo incompleto (codice 43) bloccano la creazione. La protezione non si può disattivare e gli avvisi restano visibili anche con --silent e --yes. Il controllo ZipBomb descritto qui riguarda gli ZIP, non gli altri formati.

### Esempi

```
zg -sfx documenti.zip documenti.exe
```

```
zg -sfx documenti.7z documenti.exe --message="Documenti del progetto" --file-list=expanded
```

```
zg -sfx foto.zip foto.exe --destination=pictures --subfolder=Vacanze --overwrite
```

```
zg -sfx progetto.zip progetto.exe --run-after=istruzioni.txt --icon=progetto.ico
```

Per conoscere i codici finali usa zg -help -exitcodes.

Il necessario per creare lo SFX è già incluso in zgx. Il comando funziona anche nella versione console standalone: non serve installare l’app principale o procurarsi sfx\_stub.exe. La copia temporanea usata durante la creazione viene rimossa al termine.

### Piattaforme e nomi dei file

Su Windows il risultato è un file .exe. Su Linux è un eseguibile, per esempio documenti.run; se necessario, prima di aprirlo usa chmod +x documenti.run. Su macOS la variante grafica è un’app .app, mentre quella terminale è un eseguibile.

Per creare e firmare localmente un pacchetto macOS occorre un Mac. Questa firma locale non sostituisce la firma e la notarizzazione richieste per una distribuzione pubblica su macOS.

Ogni distribuzione di zgx include i modelli preparati in fase di compilazione, senza file stub esterni. Se il modello scelto non è incluso, il comando lo segnala: non genera un pacchetto per un altro sistema al suo posto.

L’icona personalizzata è disponibile solo con --ui=gui. Per personalizzare l’icona della GUI Windows x64 occorre creare il pacchetto su Windows.

### Usare il pacchetto terminale

Avvialo e segui le domande. Il pacchetto mostra la cartella proposta, chiede conferma e richiede la password solo quando serve. Ctrl+C annulla; la password digitata non viene mostrata.

Per un’operazione automatica puoi usare: ./documenti.run --yes --destination=/percorso/estratti --no-open

--overwrite consente di sostituire i file estratti già esistenti. --list mostra l’elenco. --no-open evita di aprire Leggimi o altri file al termine. --password=valore fornisce una password senza domande; negli usi interattivi è preferibile digitarla quando richiesta.

Una destinazione fissata dall’autore non può essere cambiata con --destination.

### Esempi per altre piattaforme

```
zg -sfx documenti.zip documenti.exe --ui=terminal --target=win-x64
```

```
zg -sfx documenti.zip documenti.run --ui=terminal --target=linux-x64
```

```
zg -sfx documenti.zip documenti.run --ui=gui --target=linux-x64
```

```
zg -sfx documenti.zip Documenti.app --ui=gui --target=osx-arm64
```

Per vedere i modelli inclusi nella tua copia di zgx usa: zg -sfx --list-targets

<a name="test"></a>

## `-test` alias -t

`Sintassi:`

`zg -test archivio [Ppassword] [--verbose] [--silent]`

`Alias: zg -t archivio`

Controlla se i file di un archivio possono essere letti e se i controlli di integrità disponibili risultano corretti. È utile, per esempio, prima di conservare un backup o dopo aver scaricato un archivio.

Il comando legge i dati senza salvarli come file estratti. Non modifica l'archivio e non chiede una cartella di destinazione. Anche un archivio vuoto valido supera il test.

### Opzioni

Ppassword Password dell'archivio. Puoi usare anche --password=valore.

--verbose Mostra gli elementi controllati e i dettagli disponibili.

--silent Non mostra intestazione, avanzamento o messaggio di riuscita. Gli errori e gli avvisi di sicurezza restano visibili.

--noprogress Nasconde l'indicatore di avanzamento.

Se serve una password, nell'uso interattivo viene richiesta senza mostrare i caratteri digitati. Puoi riprovare fino a tre volte; Esc annulla. Con --silent o con l'input proveniente da uno script non vengono fatte domande: fornisci la password nel comando.

Una password errata e dati cifrati danneggiati non sono sempre distinguibili: in entrambi i casi il test non viene dichiarato riuscito.

### Protezione dagli archivi pericolosi

Il controllo ZipBomb è sempre attivo, anche per -test: verificare l'integrità richiede di decomprimere i dati e potrebbe consumare molte risorse. Un semplice avviso consente di continuare. Un rischio alto o un controllo incompleto fermano il test prima della decompressione. --yes e --silent non permettono di superare un blocco.

### Codici finali per gli script

0 Test completato, nessun errore rilevato dai controlli disponibili.

3 Archivio non trovato.

6 Test fallito: dati o struttura dell'archivio danneggiati o non leggibili.

7 Formato o funzione di controllo non supportati dal motore disponibile.

8 Dati protetti non verificati: password mancante o errata, oppure dati cifrati danneggiati.

20 Operazione annullata.

30 Problema di accesso al file o al motore di controllo.

42 Test bloccato per rischio ZipBomb alto.

43 Test bloccato perché il controllo di sicurezza non è completo. Può succedere anche quando le intestazioni ZIP sono danneggiate.

99 Sintassi o opzione non valida.

Un avviso di rischio moderato non decide il risultato finale: se il successivo test riesce, il codice è 0; se trova un problema, restituisce il relativo codice di errore.

In PowerShell leggi $LASTEXITCODE subito dopo il comando. Nel terminale Linux o macOS usa $?.

### Formati e limiti

Gli ZIP vengono controllati con motori gestiti su tutte le piattaforme, anche quando sono protetti con AES. Su Windows gli altri formati leggibili dal motore 7-Zip, tra cui 7Z, RAR, CAB e TAR, usano il suo test interno. Su Linux e macOS viene usato il lettore gestito per i formati che riconosce, tra cui 7Z, RAR, TAR e GZIP; alcuni altri formati possono restituire 7.

Il test SQX non è ancora disponibile e restituisce 7: il comando non estrae i file come ripiego.

I controlli dipendono dal formato. Per esempio, TAR non contiene un codice di controllo dei dati dei singoli file: non tutti i cambiamenti al contenuto possono essere rilevati. Il test non è un antivirus e non controlla ricorsivamente gli archivi contenuti come normali file dentro un altro archivio.

### Esempi

```
zg -test documenti.zip
```

```
zg -t backup.7z --verbose
```

```
zg -test protetto.zip Psegreta --silent
```

```
zg -test protetto.zip --password=segreta --noprogress
```

### Esempio di controllo automatico in PowerShell

```
zg -test "backup.zip" --silent
$esitoTest = $LASTEXITCODE
if ($esitoTest -eq 0) { "Archivio verificato" } else { "Controllo non riuscito: codice $esitoTest" }
```

### Esempio nel terminale Linux o macOS

```
zg -test backup.zip --silent
esito_test=$?
if [ "$esito_test" -eq 0 ]; then echo "Archivio verificato"; else echo "Controllo non riuscito: codice $esito_test"; fi
```

Un codice diverso da 0 richiede attenzione: non significa sempre che i file siano danneggiati. Potrebbe mancare la password, il formato potrebbe non essere supportato oppure la protezione di sicurezza potrebbe aver fermato il controllo.

<a name="info"></a>

## `-info`

Informazioni sui file: -info  
Sintassi: zg -info file [--verbose] [--silent]

Mostra in tabella le informazioni lette dal file. Apre il file in sola lettura: non monta le immagini, non estrae file e non modifica l'originale.

Il rapporto normale mostra il percorso completo, la dimensione esatta in byte, l'ultima modifica in UTC e il lettore del formato. Dove possibile il riconoscimento usa il contenuto; l'estensione può scegliere il lettore, ma non dimostra che il file sia valido.

Per le immagini ISO e UDF mostra filesystem, etichetta del volume, dimensione e numero dei settori e, per ISO 9660, le informazioni principali del volume e la presenza di un'immagine di avvio.  
Per VHDX, VHD, VMDK, VDI, DMG e immagini RAW/IMG/IMA mostra capacità virtuale, tipo di disco, partizioni, volumi e filesystem riconosciuti. Dimensione del file e capacità virtuale sono valori diversi: un disco dinamico può occupare molto meno della sua capacità. La geometria è un valore di compatibilità, non una seconda misura della capacità.  
Per gli archivi supportati dal lettore disponibile mostra numero di file e cartelle, dimensioni dichiarate e informazioni sulla cifratura. Su Windows può usare anche il lettore 7-Zip incluso per altri formati di archivio.

--verbose aggiunge tutti i metadati semplici esposti dai lettori: livelli del disco, spazio dinamico, necessità di dischi padre, identificativi, geometria, dettagli del filesystem e informazioni specifiche del formato. Per VHDX comprende dimensione dei blocchi, settori logici e fisici, programma creatore e identificativi delle intestazioni. Per VMDK comprende tipo di creazione, adattatore e percorsi delle parti, quando disponibili. Per ISO aggiunge editore, preparatore, applicazione, insieme di volumi, date e dettagli di avvio. Per gli archivi aggiunge una tabella delle voci con dimensioni, cifratura, codice di controllo e data di modifica, dove disponibili.  
Non tutti i formati o lettori forniscono ogni dato. I valori mancanti o non supportati sono indicati come non disponibili. I codici di controllo mostrati sono quelli salvati nel file, non una nuova verifica: per controllare l'integrità degli archivi usa -test. Il comando non esamina tutti i file di un disco e non calcola un'impronta dell'intero file. Non è una scansione antivirus.

Per dischi divisi in più parti o dipendenti da un disco padre conserva tutti i file nelle posizioni previste. Parti mancanti, funzioni non supportate o metadati non leggibili possono impedire un rapporto completo. Un filesystem non riconosciuto può essere vuoto, cifrato o non supportato: non è una prova di danneggiamento. Linux e macOS usano i lettori gestiti; i formati leggibili possono differire da Windows.

Le tabelle usano nomi di campo inglesi stabili, conteggi esatti in byte e numeri nello stesso formato su ogni sistema. Tabulazioni e caratteri di controllo nei nomi sono sostituiti con spazi. --silent nasconde l'intestazione del programma ma conserva il rapporto; --verbose funziona anche con --silent. I problemi vengono scritti su stderr e le tabelle su stdout: puoi quindi salvare il rapporto in un file di testo.

Il controllo di sicurezza ZIP resta attivo. Come -list, -info può mostrare i metadati di un archivio sospetto e restituisce il relativo codice di rischio. Non ne estrae i contenuti.

Codici finali:  
0: rapporto completato con i metadati disponibili.  
3: file o componente necessario non trovato.  
7: formato o funzione non supportati.  
9: rapporto incompleto o metadati illeggibili/danneggiati. Le tabelle già stampate restano utili, ma non costituiscono un risultato completo.  
30: accesso negato.  
41: avviso di rischio ZIP.  
42: rischio ZIP alto.  
43: controllo di sicurezza ZIP incompleto.  
99: argomenti non validi.  
Un errore nella lettura dei metadati ha precedenza sui codici di rischio ZIP; gli avvisi di sicurezza restano su stderr. Un rapporto ZIP completato restituisce il codice di rischio se diverso da zero.

Esempi:  
zg -info installazione.iso  
zg -info macchina.vhdx --verbose  
zg -info macchina.vmdk --verbose --silent > rapporto.txt  
zg -info backup.zip --verbose  
Guida: zg -help -info

Le intestazioni cifrate degli archivi possono impedire la lettura: questo comando non richiede una password. Il motore SQX non fornisce i metadati e restituisce 7. Gli identificativi aggiuntivi VDI sono letti per la versione 1.1; per altre versioni vengono mostrati i dati disponibili dal lettore.