# ZipGenius X SFX

Windows x64: compilare con CMake e Visual Studio. Il progetto produce soltanto
`sfx_stub.exe`, senza generatore CLI.

```powershell
cmake -S sfx_stub -B sfx_stub/build -A x64
cmake --build sfx_stub/build --config Release
```

La copia compilata usata da ZipGenius X è `sfx_stub/template/sfx_stub.exe`.
Dopo modifiche ai sorgenti C++, aggiornare quella copia con l'eseguibile Release.
Il progetto dell'app la distribuisce come `sfx_stub.exe` per x64.

ZipGenius X incorpora direttamente archivio ZIP, 7z o RAR e opzioni SFX nella
copia dello stub tramite `ZipGenius_X.Utils.SfxPackageWriter.Create`. L'API
usa `sfx_stub.exe` accanto all'app; un overload accetta un percorso dello stub
esplicito per i test. Riceve archivio, EXE di destinazione e `SfxPackageOptions`
(avvio automatico, elenco file, Leggimi, file da eseguire, icona `.ico`, messaggio).
L'elenco file può essere assente (impostazione predefinita), visibile o inizialmente
collassato. Negli ultimi due casi l'utente può aprirlo e richiuderlo nello stub.
L'opzione `CloseOnSuccess` è attiva per impostazione predefinita e chiude lo SFX
al termine di un'estrazione riuscita. Lo stub usa il selettore cartelle di Windows,
che consente anche di creare una nuova cartella, e mostra una barra di avanzamento
durante l'estrazione.
La destinazione può restare modificabile nello SFX oppure essere fissata a una
cartella di sistema (Desktop, Documenti, Download, Immagini, Musica, Video o
Preferiti), con un sottopercorso relativo facoltativo. La cartella di sistema
viene risolta sul computer dove si apre l'SFX; con la destinazione fissa lo
stub mostra il percorso senza permettere di modificarlo o sfogliarlo.
Il titolo della finestra contiene il nome dell'archivio originale. Il messaggio
predefinito è `Questo è un archivio autoestraente creato con ZipGenius X.`.
La barra mostra l'avanzamento comunicato da 7-Zip e, nella fase finale, i byte
copiati nella destinazione.
Il testo dell'interfaccia usa Segoe UI a 9 punti, il titolo mantiene 17 punti.
Sotto i pulsanti compare il copyright a 8 punti in grassetto, con anno corrente
e collegamento a zipgenius.it.
Se `Message` non viene specificato, viene usato il messaggio predefinito;
`Message` permette a ZipGenius X di sostituirlo quando sarà disponibile il flusso SFX.
Non avvia altri programmi. Il flusso SFX nell'interfaccia di ZipGenius X non è
ancora presente.

La password non viene salvata nel pacchetto: lo stub la richiede solo quando
necessaria per aprire l'archivio cifrato. L'output è un solo EXE; a runtime il
motore 7-Zip viene estratto in una cartella temporanea e cancellato alla chiusura.
Lo stub rileva anche le voci `Encrypted = +` negli ZIP con elenco leggibile:
prima di estrarre o aprire il Leggimi chiede la password. Se alcuni file usano
password diverse, conserva quelli estratti nella cartella temporanea e chiede
un'altra password, riprovando soltanto i file con errori espliciti di password.
Non verifica l'intero archivio con una sola password. Annulla interrompe senza
copiare risultati parziali nella destinazione; gli altri errori restano errori.
`sfx_stub/Test-EncryptedArchives.ps1` verifica il comportamento del motore con
ZIP ZipCrypto/AES e 7z con intestazioni cifrate.

I nomi con spazi o punti finali sono accettati: il motore Windows li sostituisce
con `_`. Prima dell'estrazione lo stub verifica anche i percorsi risultanti e
rifiuta collisioni fra nomi o cartelle (senza distinzione maiuscole/minuscole),
traversal e nomi riservati. Questi errori mostrano un messaggio sui percorsi,
senza chiedere inutilmente la password. La normalizzazione non modifica lo ZIP.
Eseguire `ctest --test-dir sfx_stub/build -C Release --output-on-failure` per
i test; `sfx_path_tests.exe elenco.txt` verifica anche un elenco `7z l -slt -sccUTF-8`.

La copia e l'eventuale sostituzione dell'icona modificano comunque l'EXE
finale. Una firma digitale dello stub modello non rimane valida nel file SFX:
se si firma il prodotto, occorre firmare l'EXE finale dopo la creazione.

Il motore incluso è 7-Zip (LGPL e restrizione unRAR). Il testo della licenza è
in `third_party/License.txt`; sorgenti e informazioni: https://www.7-zip.org/.
