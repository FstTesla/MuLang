# Scelte autonome da revisionare

## 1. Nuove API pubbliche

Sono state aggiunte le seguenti API pubbliche:

- `TypeRelations.IsCastable`, che espone la relazione statica di ammissibilità dei cast;
- `IrConversionKind` e `IrInstruction.Convert.Kind`, che permettono agli exporter di distinguere i cast dalle conversioni.

Le API sono documentate come nuove funzionalità nel changelog della versione `0.2.0-alpha.2`.

L'alternativa consiste nel mantenere la distinzione interna al compilatore e nel duplicare o ricostruire la relativa logica nel validatore IR e negli exporter.
