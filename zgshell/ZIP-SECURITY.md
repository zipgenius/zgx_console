# Protezione dagli archivi pericolosi

Uno ZIP può essere molto piccolo ma richiedere uno spazio enorme quando viene aperto: questo tipo di archivio viene chiamato "ZipBomb" e può riempire il disco o bloccare il computer.

zg controlla gli ZIP automaticamente. La protezione è sempre attiva: non occorre impostarla e non può essere disattivata. Anche --silent e --yes lasciano attivi i controlli e gli avvisi di sicurezza.

Un avviso indica un sospetto: l'operazione può continuare. Un rischio alto blocca l'operazione. Se il controllo non può essere completato, zg si ferma per prudenza; questo non significa necessariamente che il file sia pericoloso.

Se compare un blocco, evita di riprovare alla cieca: chiedi una nuova copia del file a chi te lo ha fornito o usa una copia attendibile. Alcuni ZIP protetti che contengono altri ZIP possono non essere controllabili.

Il controllo riguarda gli ZIP e i file basati sul formato ZIP, anche se rinominati. Non è una verifica antivirus e un punteggio basso non garantisce che qualsiasi contenuto sia innocuo.

## `-add`

Prima di creare o aggiornare l'archivio, zg controlla lo ZIP già esistente e gli ZIP scelti da aggiungere, anche nelle sottocartelle incluse. Con un avviso può continuare; con rischio alto o controllo incompleto si ferma prima di modificare l'archivio o spostare i file originali.

## `-extract`

Prima di estrarre, zg controlla lo ZIP. Un avviso consente di continuare; un rischio alto o un controllo incompleto bloccano l'estrazione prima di creare la cartella di destinazione. Il controllo riguarda l'intero ZIP, anche quando scegli di estrarre soltanto alcuni file.

## `-list`

Mostra i nomi dei file senza estrarli sul disco e segnala eventuali sospetti. Puoi quindi consultare l'elenco anche quando l'estrazione sarebbe bloccata, se l'elenco è leggibile. Il punteggio (score) indica il livello di rischio stimato: non è una certezza né una probabilità esatta.

## `-backup`

Il backup usa gli stessi controlli di -add: verifica il backup ZIP già esistente e gli ZIP scelti da salvare. Un avviso consente di continuare; rischio alto o controllo incompleto fermano il backup prima delle modifiche.

## `-convert`

La conversione deve prima estrarre il contenuto. Se lo ZIP sorgente presenta un rischio alto o non può essere controllato completamente, zg si ferma senza sostituire il file di destinazione. Un semplice avviso consente di continuare. Anche gli ZIP estratti che vengono aggiunti al nuovo archivio sono controllati. L'archivio sorgente resta invariato.

## Codici per chi usa script o operazioni automatiche

Con -list, il codice finale permette di decidere cosa fare senza leggere il messaggio:

0: nessun rischio significativo rilevato (score sotto 40), se l'elenco è stato letto correttamente.

41: avviso, score da 40 a 59.

42: rischio alto, score da 60 a 99.

43: controllo incompleto, salvo un rischio alto già rilevato.

I normali errori, come un file inesistente, mantengono i propri codici.

Con -add, -extract, -backup e -convert, 42 e 43 indicano un blocco di sicurezza. Un semplice avviso lascia proseguire: se l'operazione riesce, il codice finale è 0.

L'elenco e gli avvisi usano due uscite separate (stdout e stderr). Salvare l'elenco in un file non nasconde gli avvisi. Nei messaggi, risk-code descrive il rischio; non è sempre il codice finale dell'operazione.

## `-sfx`

Prima di creare l’archivio autoestraente, zg controlla la sorgente ZIP. Un avviso consente di continuare; rischio alto (42) o controllo incompleto (43) bloccano la creazione senza sostituire un file esistente. Gli avvisi restano visibili anche con --silent.

## `-test`

Anche il test di integrità deve decomprimere i dati per controllarli. Per questo verifica prima la sicurezza dello ZIP: rischio alto (42) e controllo incompleto (43) bloccano il test. Un avviso consente di continuare, ma non equivale a un test riuscito. Non vengono creati file estratti e non viene modificata la sorgente.
