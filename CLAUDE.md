# CLAUDE.md

Leitfaden für Claude Code in diesem Repository. Die Regeln sind verbindlich. Bei einem Konflikt mit einer Aufgabenstellung wird nachgefragt, statt eine Regel stillschweigend zu brechen.

Maßgebliche Quelle ist das Konzept „Nero PDF – Konzept und Entwicklungsleitfaden“ (Claude Doc: https://claude.ai/artifact/PG4S8SXHiVoQtAHzPZr7iw). Entscheidungen, die das Konzept betreffen, werden dort nachgetragen. Kommunikation mit Stefan auf Deutsch.

## Produkt in einem Satz

Nero PDF ist ein schlanker PDF-Viewer und virtueller Drucker für Windows und macOS mit genau diesen Funktionen: Anzeigen, Seitenübersicht, Umsortieren, Drehen, Löschen, Speichern (inkl. passwortgeschützter PDFs), Drucken auf einen echten Drucker (Stefan, 27.11.2026), dazu zwei feste Hinweis-Kacheln auf Nero AI und Nero Platinum unten in der Seitenübersicht (URLs in JSON, Öffnen im Standardbrowser). Zusätzlich, nur freigeschaltet bei installiertem Nero Start/Nero Platinum (Stefan, 26.11.2026): „Scannen …“ (direkt als PDF) und „Konvertieren …“ (druckbare Dateien über den Drucker Nero PDF als PDF). Ohne Nero-Installation sind beide Knöpfe von „Get Nero Platinum to unlock“ überdeckt.

## Umfang und Feature-Creep-Schutz

- Nur die oben genannten Funktionen. **Keine** Formulare, Signaturen, Annotationen, OCR, Schwärzen oder Export in andere Formate, auch nicht als Vorbereitung. Konvertieren gibt es nur als Druck über Nero PDF.
- Claude ergänzt von sich aus keine Funktionen und schlägt keine zusätzlichen vor.
- Schlägt Stefan etwas vor, das über den Umfang hinausgeht, zuerst nachfragen, etwa: „Stefan, bist du sicher? Du wolltest ein schlankes Programm mit wenigen, klar definierten Funktionen.“ Erst nach Bestätigung umsetzen und im Konzept nachtragen.
- Leitbild: Im Zweifel gewinnt die einfachere Lösung.

## Arbeitsweise und Rückfragepflicht

- Bei Unklarheit fragen, nicht vermuten.
- Größere Schritte zuerst kurz beschreiben und freigeben lassen, dann bauen.
- Kein Ballast auf Verdacht: kein vorsorglicher Code, keine vorsorglichen Datenstrukturen.
- Kleine, nachvollziehbare Änderungen mit klarer Beschreibung.
- Design ist abgenommen: Entwurf A (hell, schwarzer Kopfbereich, Seitenübersicht links) mit B als Dunkelmodus. Entwürfe liegen in `design/`.
- Das Fenster verhält sich trotz eigenem Kopfbereich wie ein normales Systemfenster (Größe ändern, Snap, Doppelklick maximiert; auf macOS native Ampel-Schaltflächen).
- Entwicklungsschritte in dieser Reihenfolge, jeder wird abgenommen, bevor der nächste beginnt:
  1. Basisprojekt und Drucker (Windows, reiner Viewer) – erledigt
  2. Speichern und Öffnen (inkl. Drag and Drop, Passwortdialog)
  3. Navigation und Seitenübersicht
  4. Seitenbearbeitung (Drehen, Löschen, Umsortieren per Schaltflächen und Drag and Drop)
  5. Danach Auslieferung: signiertes MSIX aus dem lokalen Build an das Store-Team. Mac-Build und Pipeline entfallen vorerst (Stefan, 26.11.2026), solange keine Mac-Version gebaut wird.

## Plattformübergreifend von Anfang an

- .NET 10 + Avalonia 12 (Visual Studio 2026). Kein .NET Framework, kein WPF, kein MAUI.
- Windows: nur Windows 11 24H2 oder neuer, Auslieferung nur als MSIX. Der virtuelle Drucker ist eine Print Support App (PSA v4), kein Ordnerüberwacher und kein „Microsoft Print to PDF“-Drucker.
- Im gemeinsamen Code keine Windows-APIs, keine Registry, keine Laufwerksbuchstaben, keine Backslash-Pfade. Pfade nur über `Path.Combine` und die .NET-Ordnerauflösung.
- Alles Plattformspezifische als Schnittstelle im gemeinsamen Projekt, Umsetzung je Plattform im eigenen Projekt: Ordnerüberwachung, Drucker anlegen/entfernen, Temp- und Datenordner, Öffnen im Standardprogramm, Dateiverknüpfung, Autostart.
- `OperatingSystem.IsWindows()` und ähnliche Abfragen nur in Plattformprojekten oder in der zentralen Registrierung, nie im Oberflächencode.
- Bei jeder Windows-spezifischen Funktion steht in der PR-Beschreibung das macOS-Gegenstück oder warum es keines braucht.
- Der Code muss jederzeit ohne Änderung auf macOS übersetzbar sein.

## Projektstruktur

- `NeroPdf.sln`, zentrale Paketversionen in `Directory.Packages.props`.
- `src/NeroPdf.Core`: Logik ohne Avalonia-Abhängigkeit (PDF-Engine, Sprachen, Logging, Einstellungen). Seitenoperationen sind ohne Oberfläche testbar.
- `src/NeroPdf.App`: Avalonia-Oberfläche, Sprachdateien in `Languages/`.
- `src/NeroPdf.Windows` und `src/NeroPdf.MacOS`: startbare Apps (AssemblyName `NeroPdf`) mit den Plattform-Implementierungen.
- `src/NeroPdf.Windows.PrintTasks`: Hintergrundaufgabe des PSA-Druckers (CsWinRT, x64).
- `tests/NeroPdf.Tests`: xUnit v3 über `TestFiles`.
- MSIX bauen und signieren: `build/package-windows.ps1` (siehe `docs/Installation.md`).
- Sitzungsende: Schreibt Stefan „Session Ende“, gilt der Skill `.claude/skills/session-ende` (Tests, Commit und Push, lokal bauen, ZIP nur mit Build und Kurzanleitung in den OneDrive-Ordner über `build/session-end.ps1`).
- `experiments/` ist lokale Spielwiese und wird nicht committet.

## PDF-Engine

- Ausschließlich PDFium über die NuGet-Pakete von Benoît Blanchon (`bblanchon.PDFium.Win32`/`.macOS`), angebunden über eine eigene schlanke P/Invoke-Schicht, keine Wrapper-Bibliothek. Keine weitere PDF-Bibliothek ohne ausdrückliche Freigabe (QPDF ist nur Reserve).
- PDFium hinter einer eigenen Schnittstelle (Öffnen, Rendern, Seitenliste, Drehen, Löschen, Umsortieren, Speichern). Kein direkter PDFium-Aufruf im Oberflächencode.
- Seitenoperationen wirken auf eine Arbeitskopie. Das Original bleibt unverändert, bis ausdrücklich gespeichert wird.
- Lizenztexte von PDFium und Abhängigkeiten werden mit ausgeliefert und bei neuen Paketen aktualisiert.

## Speicherlogik

- Frisch gedruckte Dokumente liegen nur im Temp-Ordner der Plattform.
- Speichern eines gedruckten Dokuments = Verschieben, nicht Kopieren.
- Eine geöffnete Datei wird beim Speichern an einem neuen Ort abgelegt, das Original bleibt erhalten.
- Speichern unter gleichem Namen am gleichen Ort muss funktionieren, ohne „Datei wird verwendet“: erst in eine Zwischendatei schreiben, Anzeige gibt die Datei frei, dann ersetzt die Zwischendatei das Original in einem Zug.
- Beim Schließen eines ungespeicherten Dokuments immer eine Rückfrage.
- Nie gespeicherte Temp-Dateien nach konfigurierbarer Frist aufräumen.
- Gedruckte Dateien erst öffnen, wenn sie vollständig geschrieben und nicht gesperrt sind. Dateinamen eindeutig (parallele Druckaufträge).

## Oberfläche und Reaktionsfähigkeit

- Die Oberfläche blockiert nie. Laden, Rendern, Seitenoperationen und Speichern laufen im Hintergrund.
- Fortschritt nur bei längeren Vorgängen, kurze Vorgänge zeigen bewusst keinen Fortschrittsbalken.
- Große Dokumente werden nach Bedarf und schrittweise gerendert.
- Grundlayout: schlank und modern, oben ein leicht grafischer Bereich mit Nero-Logo, darunter der Inhalt.
- Layouts vertragen lange Übersetzungen und Rechts-nach-links-Schrift, keine festen Breiten.

## Mehrsprachigkeit

- Jeder sichtbare Text läuft von der ersten Zeile an über die Übersetzungsschicht. Kein Text fest im Code oder in der Oberfläche, auch nicht vorläufig.
- Eigene JSON-Lösung, keine Resx-Dateien. Eine JSON-Datei je Sprache mit stabilen Text-IDs. Texte nur über eine zentrale Funktion laden. Platzhalter im Stil `{0}`, `{1}`, gefüllt per `String.Format`.
- Keine feste Sprachliste im Code: Sprachen ergeben sich aus den vorhandenen Dateien. Auswahl: exakte Kultur (z. B. `en-GB`), sonst reine Sprache (`en`), sonst Englisch. Fehlt ein einzelner Text, Rückfall auf Englisch.
- Claude übersetzt selbst: Jeder neue Text entsteht zuerst auf Englisch und wird sofort in **alle** Sprachdateien übersetzt, keine englischen Platzhalter. Ein Text existiert nie nur in einer Datei. Eine Prüfung meldet fehlende Schlüssel.
- Startsprachen (24, keine RTL): en, cs, da, de, el, en-GB, es, fi, fr, hu, it, ja, ko, nl, nb (Norwegisch), pl, pt, pt-BR, ru, sv, th, tr, zh, zh-TW.
- Gilt auch für Fehlermeldungen, Rückfragen, Menüs, den Druckernamen und das Installationspaket. Logeinträge bleiben immer Englisch.

## Logging und Fehlersuche

- Zentrale Logging-Schnittstelle, keine verstreuten Konsolenausgaben. Jede Komponente loggt wichtige Schritte, Entscheidungen und Fehler.
- Debug: ausführlich. Produktiv: sparsam, ausführliches Logging nur per Parameter. Aufwendige Log-Texte nur bauen, wenn die Stufe aktiv ist.
- Bei Bugs zuerst das Log lesen und auswerten, nicht mutmaßen. Fehlt Information, zuerst Logging ergänzen.
- Keine Dokumentinhalte und keine Passwörter loggen, bei Dateipfaden zurückhaltend sein.

## Code-Struktur

- Eine Klasse, eine Verantwortung. Klassen mit vielen Aufgaben sind ein Warnsignal und werden vor dem Weiterbau besprochen.
- Daten und Code getrennt: Konfiguration, Texte, Standardwerte, Farben und Maße als JSON-Dateien neben dem Code.

## Qualität und Tests

- Feste Testsammlung von rund 20 bis 30 PDFs im Ordner `TestFiles` (nicht ausgeliefert, nicht Teil des Builds). Die Tabelle mit dem erwarteten Verhalten je Datei steht im Konzept und ist für die Tests verbindlich.
- Nach jedem Build läuft ein automatischer Test über die gesamte Sammlung. Ab Schritt 4 auch Drehen, Löschen, Umsortieren; das Ergebnis muss ladbar bleiben.
- Das Passwort der Testdatei `sample-password-protected.pdf` steht nur im Testprojekt, nie im Programmcode oder in der Konfiguration. Passwörter werden nie gespeichert oder geloggt.
- Fehler beim Öffnen oder Speichern führen zu einer verständlichen Meldung, nie zu Datenverlust. Die Originaldatei wird nie beschädigt.
- Die Startzeit bis zur ersten angezeigten Seite wird gemessen und darf sich nicht unbemerkt verschlechtern.

## Build und Auslieferung

- Jeder Stand muss lokal unter Windows bauen und starten (Hauptziel der Entwicklungsphase).
- Mac-Build und GitHub-Actions-Pipeline erst, wenn eine Mac-Version gebaut wird. Bis dahin reicht der lokale Build.
- Zertifikate und Zugangsdaten gehören nie ins Repository, sondern in die Pipeline-Secrets.
