# Guide incorporate nella console

Modificare i file UTF-8 in questa cartella e ricompilare:

- `*.txt`: inglese e ripiego per le lingue non disponibili.
- `it-IT/*.txt`: italiano.
- `startup.txt`: schermata sintetica senza argomenti e corpo di `zg -help`.
- `generichelp.txt`: manuale generale esteso, conservato separatamente.

Tutti i `.txt`, compresi i manuali preesistenti, sono EmbeddedResource nel progetto
`zg.csproj`. Il caricamento usa queste risorse, senza leggere file accanto all'app
e senza copiarle in output/publish. Funziona anche con publish single-file.
La scelta segue CurrentUICulture e le sue culture parent; una lingua neutra o
un'altra variante regionale può usare la traduzione regionale disponibile
(`it` e `it-CH` usano `it-IT`), poi si ripiega sull'inglese.

I marcatori di formato rimangono quelli esistenti: `^` sintassi, `*` esempio,
`[` titolo, `]` parametro, `%` evidenza. Una riga `>>` è una pausa solo su terminale
interattivo, senza comparire nell'output rediretto.

I `.resx` contengono i messaggi del programma, non le guide. Per aggiungere un
comando collegare la pagina a ShowHelp/ShowManual: il wildcard MSBuild incorpora
automaticamente i nuovi file, comprese eventuali nuove cartelle lingua.

## Confronto e migrazione del 27 ottobre 2026

Prima di cambiare i file sono stati confrontati i sei testi `InlineHelp*` e la
schermata `StartupHelpText` di entrambe le lingue con tutti i manuali esistenti.

| Pagina | File preesistente | Decisione |
| --- | --- | --- |
| backup | Più dettagliato: ricorsione, rifiuto riuso, esempi sorgente e cartella utente | Conservato e integrato con l'esempio `--documents T` e la precisazione `--yes` |
| convert | Più dettagliato: cartella temporanea, password destinazione, tre esempi | Conservato e integrato con la precisazione `--yes` |
| optim | Più dettagliato: R0 predefinito, ricostruzione ZIP, backup, due esempi | Conservato senza modifiche |
| list | Assente | Creato dalla guida inline revisionata, senza ripristinare testo obsoleto |
| export | Assente | Creato dalla guida inline |
| hexview | Assente | Creato dalla guida inline |
| startup | `generichelp.txt` aveva ruolo e lunghezza diversi | Creato un file sintetico separato; conservata integralmente la guida generale |
| add, extract, dumpdisc, exitcode, hash, info, sfx, test | Già presenti in entrambe le lingue | Conservati senza modifiche |

Risultato: 20 file preesistenti invariati, 4 integrati, 8 nuovi file, 32 pagine
incorporate. Rimosse 14 voci lunghe dai resx e tre vecchie chiavi inglesi
inutilizzate che puntavano a file mancanti sotto `Resources/`.
Gli esempi importati usano normali percorsi Windows con un solo separatore.

Successivamente, nella versione 10.0.9796.35433, `it-IT/add.txt` è stato tradotto
integralmente in italiano, conservando parametri, valori, comandi di esempio e
segnapagina. `add.txt` inglese rimane invariato. I conteggi sopra descrivono la
migrazione iniziale, precedente a questa revisione linguistica.
