using Xunit;

// Die Schutzlisten (ProtectionLists) sind globaler, zur Laufzeit änderbarer
// Zustand (M8/P2-5). Tests, die ihn verändern, dürfen deshalb nicht parallel zu
// lesenden Tests laufen – sonst wären Ergebnisse von der Reihenfolge abhängig.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
