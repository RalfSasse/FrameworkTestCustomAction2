# FrameworkTestCustomAction2
Neue CustomAction für WiX-Projekt FrameworkTest (ersetzt DotNetCoreTest).

Erkennt .NET Framework- und .NET Core-Versionen auf dem Zielrechner.

Kompiliert für .NET Framework 2.0, damit es auch auf Zielrechnern mit älteren Versionen die richtigen Ergebnisse findet. Eine frühere Version dieser CustomAction war für .NET Framework 4.8 kompiliert und funktionierte nur, wenn .NET Framework 4.8 auch auf dem Zielrechner vorhanden war (was bei diesem Projekt nicht zielführend ist;-)
