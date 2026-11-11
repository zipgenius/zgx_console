# Conversione su Linux

`-convert` estrae gli archivi tramite SharpCompress e crea ZIP e TAR con le
librerie gestite del progetto. La scrittura di 7Z e TAR.XZ usa direttamente
`libarchive.so.13` tramite la sua API C: non avvia eseguibili esterni.
La distribuzione Linux deve includere questa libreria e il locale `C.UTF-8`.

Esempio: `zg -convert documenti.zip documenti.7z`

La password viene chiesta solo per archivi protetti. Le barre rispettano
`--silent` e `--noprogress`. La destinazione viene sostituita con `--overwrite`
solo dopo che la conversione è riuscita.

Limiti del motore Linux:

- Non crea 7Z cifrati o suddivisi in volumi; per la cifratura in uscita usare ZIP.
- Non converte SQX e non crea CAB, che richiedono i componenti Windows.
- Gli altri comandi storici che usano SevenZipSharp non sono migrati da questa modifica.

Verifica dalla radice del repository:

```sh
dotnet build zgshell/zg.csproj
python3 zgshell/tests/linux_conversion_smoke.py
python3 zgshell/tests/linux_conversion_interactive.py
```

Il secondo test Python richiede `pexpect` e verifica richiesta password,
tentativo errato, avanzamento, archivi danneggiati e conservazione della
destinazione in caso di errore.

## Verificare un archivio senza estrarlo

Usa `zg -test archivio` oppure `zg -t archivio`. Il comando legge i file senza
salvarli sul disco e senza modificare la sorgente. Per uno ZIP protetto puoi
fornire la password con `Ppassword`, oppure digitarla quando viene richiesta.
Con `--silent` non vengono fatte domande.

Il codice finale è `0` se il test riesce, `6` se fallisce, `7` se il motore non
supporta il formato e `8` se i dati protetti non possono essere verificati.
La protezione ZipBomb resta sempre attiva e può fermare il controllo con `42`
o `43`. Tutti i codici e gli esempi sono disponibili con `zg -help -test`.

Il percorso Linux è stato verificato anche con ZIP AES e 7Z solidi o con elenco
cifrato. SQX non è ancora supportato dal test. Per formati senza un codice di
controllo dei dati, come TAR, il test non può rilevare ogni modifica ai file.

## Informazioni su file e immagini disco

`zg -info file [--verbose]` legge archivi e immagini ISO/UDF, VHDX, VHD, VMDK, VDI, DMG e RAW con i lettori gestiti, senza montare o modificare i file. Mostra capacità, partizioni e filesystem riconosciuti; `--verbose` aggiunge i metadati disponibili. Le intestazioni delle tabelle sono inglesi e i conteggi in byte sono esatti. Il codice 9 indica un rapporto incompleto. Per opzioni e limiti: `zg -help -info`. I lettori possono non supportare alcune varianti, dischi cifrati o catene con componenti mancanti.
