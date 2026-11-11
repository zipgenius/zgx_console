# SFX portabili

`zgx` può creare pacchetti con una finestra (`--ui=gui`) oppure con domande
nel terminale (`--ui=terminal`). La piattaforma di chi **aprirà** il pacchetto si
sceglie con `--target`, indipendentemente dal formato dell'archivio sorgente.
Per impostazione iniziale usa GUI, sistema operativo e processore del computer
che crea il pacchetto.

| Destinazione | Terminale | GUI |
|---|---|---|
| Windows x64 | .NET autonomo | Stub Win32 già usato dalla GUI principale |
| Windows ARM64 | .NET autonomo | Avalonia autonomo |
| Linux glibc x64 / ARM64 | .NET autonomo | Avalonia autonomo, richiede desktop |
| macOS Intel / Apple Silicon | .NET autonomo | Avalonia autonomo, pacchetto `.app` |

Il destinatario non deve installare zgx, .NET o 7-Zip. La variante grafica
Linux usa le librerie del desktop (X11, fontconfig e relative dipendenze).
La variante terminale funziona senza un desktop. Non si sostituisce una GUI
richiesta con domande nel terminale senza segnalarlo.

## Compilazione e incorporamento

Una normale build di `zgx/zg.csproj` prepara automaticamente gli stub del
runtime scelto, li comprime e li incorpora nell'assembly. Il Win32 x64 esistente
rimane incorporato a parte. Il compilatore degli stub è un piccolo programma
.NET in `tools/BuildSfxTemplates`; non richiede PowerShell o Python sulla
macchina di compilazione. Gli artefatti intermedi Unix sono conservati nella
cache dell'utente, su filesystem nativo.

Per preparare più piattaforme prima della build di zgx:

```text
dotnet run --project tools/BuildSfxTemplates -c Release -- --root . --rids win-x64,linux-x64 --output SfxPortable/templates
dotnet build zgx/zg.csproj
```

La compilazione degli stub per un'altra piattaforma è una pubblicazione .NET
self-contained. Tutti gli stub già presenti vengono aggiornati se cambiano i
sorgenti condivisi. `templates/*.gz` sono artefatti generati, non sorgenti Git.
Per una build di sviluppo si può evitare di rigenerarli con
`-p:BuildSfxTemplates=false`; vengono comunque incorporati quelli presenti.
La proprietà `SfxTemplateRids` seleziona i runtime da preparare automaticamente;
`SfxTemplatesDir` consente a una pipeline di fornire una directory dedicata.

```text
zg -sfx --list-targets
zg -sfx documenti.zip documenti.exe --ui=terminal --target=win-x64
zg -sfx documenti.zip documenti.run --ui=gui --target=linux-x64
zg -sfx documenti.zip Documenti.app --ui=gui --target=osx-arm64
```

Se manca uno stub, il comando fallisce e mostra quelli disponibili; non cerca
eseguibili in rete o accanto all'app e non genera un pacchetto per un sistema
diverso. La personalizzazione dell'icona Win32 richiede Windows. La finalizzazione
di pacchetti macOS richiede un Mac: imposta i permessi eseguibili e applica una
firma ad-hoc con `codesign`. Firma Developer ID e notarizzazione della
distribuzione pubblica restano un passaggio di rilascio distinto.

## Motore e sicurezza

`Core` condivide formato, controllo SHA-256, lettura ZIP/7Z/RAR tramite
SharpCompress e staging dell'estrazione fra terminale e GUI. Il trailer portabile
`ZGXSFXP1` è distinto dal footer Win32 esistente. Il lettore accetta una firma
macOS dopo il trailer, senza richiedere di modificare il codice Win32.

La protezione ZipBomb è attiva sia alla creazione sia nell'estrattore portabile.
Non si possono disattivare i blocchi con opzioni di conferma o modalità silenziosa.
Gli archivi estratti non vengono a loro volta espansi automaticamente.
Vengono rifiutati percorsi che escono dalla destinazione, nomi ambigui e
scritture attraverso collegamenti simbolici. Le password non sono salvate nel
pacchetto. La destinazione è pubblicata dopo l'estrazione in staging; errori di
password o annullamenti durante lo staging non pubblicano file incompleti.

## Verifica

```text
dotnet build SfxPortable/Desktop/Sfx.Desktop.csproj
dotnet build zgx/zg.csproj
python zgx/tests/sfx_smoke.py
python zgx/tests/sfx_portable_smoke.py
python zgx/tests/zipbomb_security.py
```

Il test portabile esegue davvero lo SFX terminale Windows e, con WSL Ubuntu-26.04
e i template Linux, anche la variante terminale Linux e il flusso automatico
della GUI in WSLg. I test macOS e ARM64 richiedono runner delle rispettive
piattaforme; una cross-compilazione non ne conferma il funzionamento sul dispositivo.

La GUI portabile usa [Avalonia](https://docs.avaloniaui.net/); la variante Win32
mantiene il proprio motore 7-Zip e la licenza già distribuita. Conservare gli
avvisi di licenza delle dipendenze quando si prepara la distribuzione.
