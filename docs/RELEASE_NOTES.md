# ZipGenius X Console 10.0.2

## Italiano

- `-list` mostra i membri esterni dei pacchetti DEB, compreso `debian-binary`, e i file contenuti nei TAR compressi. I flussi BZ2, XZ e Z privi di nome usano il nome dell'archivio senza estensione.
- `-export` usa lo stesso lettore di `-list`. Su Windows è stato corretto il crash del lettore CAB; i metadati non disponibili sono indicati come tali.
- Su Linux libgcab abilita creazione, aggiornamento, elenco, verifica, estrazione e conversione dei CAB. La creazione usa C0 senza compressione o MSZIP con gli altri livelli; LZX è supportato in lettura. I CAB suddivisi in volumi non sono supportati.
- Su Linux wimlib abilita creazione, aggiornamento, elenco, verifica, estrazione e conversione WIM a immagine singola. L'estrazione rifiuta collegamenti e reparse point; password e volumi suddivisi non sono supportati in creazione.
- I pacchetti Linux DEB, RPM e Arch dichiarano le dipendenze native. Per la versione portabile vanno installate separatamente; Snap e Flatpak non includono ancora libgcab e wimlib. SQX resta escluso dalle modifiche.

Il test DEB controlla struttura e lunghezze dei membri esterni: ar non fornisce checksum per membro e gli archivi interni non sono verificati ricorsivamente. I controlli ZIP di sicurezza restano attivi.

## English

- `-list` displays the outer members of DEB packages, including `debian-binary`, and entries inside compressed TAR archives. Nameless BZ2, XZ and Z streams use the archive filename without its extension.
- `-export` shares the list reader. A Windows CAB reader crash was fixed, and unavailable metadata is reported as unavailable.
- Linux libgcab enables creating, updating, listing, testing, extracting and converting CAB archives. C0 stores data and other levels use MSZIP; LZX is supported for reading. Split cabinets are unsupported.
- Linux wimlib enables creating, updating, listing, testing, extracting and converting single-image WIM files. Extraction rejects links and reparse points; creation does not support passwords or split volumes.
- Linux DEB, RPM and Arch packages declare the native dependencies. Portable distributions require them separately; Snap and Flatpak do not yet bundle libgcab or wimlib. SQX remains outside this change.

DEB testing checks the outer ar structure and member lengths: ar has no per-member checksums and nested archives are not tested recursively. ZIP safety checks remain active.
