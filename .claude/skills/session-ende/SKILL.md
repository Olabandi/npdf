---
name: session-ende
description: Abschluss einer Arbeitssitzung an Nero PDF. Verwenden, wenn Stefan "Session Ende" (oder "Sitzung beenden", "Feierabend") schreibt. Testet, committet und pusht, baut das signierte MSIX-Paket lokal und legt ein ZIP mit Build und Anleitung (ohne Quellcode) in seinen OneDrive-Ordner.
---

# Session Ende

Stefans Ablauf zum Ende einer Sitzung. Der Aufruf mit „Session Ende“ ist seine Freigabe für Commit und Push.

1. **Prüfen**
   - `dotnet build NeroPdf.sln` muss ohne Fehler und Warnungen durchlaufen.
   - `dotnet test --solution NeroPdf.sln` muss grün sein.
   - Schlägt etwas fehl: nicht committen, Stefan das Problem nennen und fragen, wie es weitergeht.

2. **Commit und Push**
   - `git status` ansehen. `experiments/` wird nie committet, ebenso keine Zertifikate, Passwörter oder `artifacts/`.
   - Alles Übrige committen, mit einer kurzen englischen Commit-Nachricht, die die Änderungen der Sitzung zusammenfasst.
   - `git push origin main`.

3. **Lokal bauen und ZIP ablegen**
   - `powershell -ExecutionPolicy Bypass -File build\session-end.ps1`
   - Das Skript erhöht die Versionsnummer selbst (letzte Stelle +1 gegenüber dem höchsten ZIP im Zielordner bzw. der installierten Version), baut und signiert das MSIX mit dem Zertifikat „Nero AG“, installiert es lokal und legt `NeroPdf_<Version>.zip` in `%USERPROFILE%\OneDrive\Stefan\Geschäftlich\Olabandi\Nero PDF` ab.
   - Inhalt des ZIP: nur das MSIX und `Kurzanleitung.txt` (Voraussetzungen, Installieren, Benutzen, Deinstallieren, Protokoll). Kein Quellcode, keine weiteren Dateien.
   - Soll eine bestimmte Version entstehen (z. B. neuer Entwicklungsschritt): `-Version 0.5.0.0` mitgeben.

4. **Abschluss**
   - Stefan in einer kurzen Nachricht nennen: Commit-Hash, Version, Pfad und Größe des ZIP, Testergebnis.
   - Steht in `docs/Kurzanleitung.txt` oder `docs/Installation.md` etwas Veraltetes, vorher aktualisieren und mitcommitten.
