# Scelte autonome da revisionare

## 1. Test `is` staticamente impossibili

I test `is` staticamente impossibili restano validi.

Di conseguenza, `1 is float` compila e restituisce `false`, mentre `1 as float` produce un errore di compilazione. L'equivalenza tra i due operatori è quindi definita soltanto per i cast `as` staticamente permessi.

L'alternativa consiste nel diagnosticare anche i test `is` per i quali il corrispondente cast non è staticamente permesso.

Questa è la scelta da revisionare con maggiore attenzione: preserva la flessibilità e il comportamento precedente di `is`, ma limita l'equivalenza alla semantica runtime delle coppie di espressioni ben formate.

## 2. Validazione runtime di ogni `as`

Ogni espressione `as` sorgente è rappresentata esplicitamente come cast controllato e percorre la stessa validazione di conformità runtime usata da `is`.

Questo vale anche per cast sicuramente validi in base ai tipi statici. La scelta rende il comportamento uniforme e impedisce divergenze tra percorsi ottimizzati e controllati, ma può consumare budget di esecuzione dove sarebbe sufficiente restituire direttamente il valore.

Gli errori di budget, cancellazione e altri errori operativi sono esclusi normativamente dalla garanzia di equivalenza.

L'alternativa consiste nell'ottimizzare i cast staticamente garantiti, preservandone il risultato senza eseguire la conformità profonda.

Questa è la scelta con il possibile impatto operativo più sottile.

## 3. Grafi ciclici nella conformità profonda

Non è stato introdotto identity tracking per la conformità profonda di oggetti e array ciclici.

`is object` e `as object` percorrono ora la stessa operazione e hanno quindi comportamento equivalente, ma un grafo ciclico o eccessivamente profondo raggiunge il limite di attraversamento anziché essere riconosciuto mediante tracciamento delle identità già visitate.

Il superamento del limite è classificato come errore operativo ed è escluso dalla garanzia di equivalenza.

L'alternativa consiste nell'aggiungere il supporto esplicito ai cicli alla validazione di conformità.

## 4. Rappresentazione dei cast nell'IR

È stata aggiunta la proprietà `IrInstruction.Convert.IsCast`, con valore predefinito `false`, senza sostituire la proprietà esistente `IsChecked`.

Questa scelta:

- preserva la compatibilità del costruttore pubblico di `IrInstruction.Convert`;
- permette agli exporter di distinguere i cast identitari dalle conversioni implicite o contestuali;
- mantiene compatibile l'IR esistente che non valorizza `IsCast`.

Il costo è la presenza di due flag parzialmente correlati.

L'alternativa consiste nel sostituire i flag con un enum pubblico che rappresenti esplicitamente la modalità della conversione, accettando la conseguente breaking change dell'API IR.

## 5. Nuove API pubbliche

Sono state aggiunte le seguenti API pubbliche:

- `TypeRelations.IsCastable`, che espone la relazione statica di ammissibilità dei cast;
- `IrInstruction.Convert.IsCast`, che permette agli exporter di distinguere i cast dalle conversioni.

Le API sono documentate come nuove funzionalità nel changelog della versione `0.2.0-alpha.2`.

L'alternativa consiste nel mantenere la distinzione interna al compilatore e nel duplicare o ricostruire la relativa logica nel validatore IR e negli exporter.
