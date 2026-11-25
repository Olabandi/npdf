# Nero PDF

Ein bewusst schlankes PDF-Werkzeug für den Alltag, für **Windows und macOS**: ein schneller Viewer und ein virtueller Drucker mit grundlegenden Seitentools.

Nero PDF startet sofort, zeigt das Ergebnis eines Druckauftrags direkt an und bietet nur die Funktionen, die man ständig braucht. Es tritt bewusst nicht in Konkurrenz zu großen PDF-Editoren.

> Status: Entwicklungsschritt 1 (Viewer und virtueller Drucker unter Windows) ist umgesetzt. Installation siehe [docs/Installation.md](docs/Installation.md).

## Funktionsumfang (Version 1.0)

- **PDF erzeugen über Druck:** Unter Windows erscheint Nero PDF als Drucker, auf dem Mac im PDF-Menü des Druckdialogs. Nach dem Druck öffnet sich das Ergebnis automatisch.
- **Viewer:** sehr schnell ladender, minimalistischer Betrachter, auch für passwortgeschützte PDFs.
- **Seitenübersicht:** Miniaturansicht aller Seiten.
- **Seiten umsortieren, drehen (90°-Schritte) und löschen**, per Schaltflächen und Drag and Drop.
- **Speichern:** Frisch gedruckte Dokumente werden an den Zielort *verschoben*, nicht kopiert. Geöffnete Dateien bleiben im Original erhalten.
- **Hinweise auf Nero AI und Nero Platinum:** zwei feste Kacheln unten in der Seitenübersicht, auffällig, aber nicht störend.

**Bewusst nicht enthalten:** Formulare ausfüllen, Signaturen, Annotationen, OCR, Schwärzen, Konvertierung.

## Speicherlogik

Ein frisch gedrucktes Dokument liegt nur im temporären Ordner der Plattform. Beim Speichern wird es an den Zielort verschoben. Wird ein ungespeichertes Dokument geschlossen, erscheint immer eine Rückfrage. Nie gespeicherte Temp-Dateien werden nach einer konfigurierbaren Frist aufgeräumt.

## Technik

| Bereich | Entscheidung |
|---|---|
| Plattform | .NET 10 (kein .NET Framework, kein WPF), Visual Studio 2026 |
| Oberfläche | [Avalonia](https://avaloniaui.net/) 12, eine Codebasis für Windows und macOS |
| PDF-Engine | ausschließlich PDFium über die NuGet-Pakete `bblanchon.PDFium`, angebunden über eine eigene schlanke P/Invoke-Schicht |
| Windows-Drucker | virtueller Drucker über Print Support App (PSA v4), Windows 11 24H2 oder neuer, Auslieferung nur als MSIX |
| macOS-Drucker | PDF-Service im Library-Ordner des Nutzers |
| Mehrsprachigkeit | eigene JSON-Lösung, eine Datei je Sprache (24 Sprachen zum Start) |
| Logging | Microsoft.Extensions.Logging mit eigener Datei-Ausgabe |
| Build | lokal unter Windows (`build/package-windows.ps1`); später GitHub Actions für MSIX und Mac-App |

### Projektstruktur

| Projekt | Inhalt |
|---|---|
| `src/NeroPdf.Core` | Logik ohne Oberfläche: PDF-Engine, Sprachen, Logging, Einstellungen |
| `src/NeroPdf.App` | Avalonia-Oberfläche und Sprachdateien (`Languages/`) |
| `src/NeroPdf.Windows` | startbare Windows-App mit den Windows-Implementierungen |
| `src/NeroPdf.Windows.PrintTasks` | Hintergrundaufgabe des virtuellen Druckers |
| `src/NeroPdf.MacOS` | startbare Mac-App mit den macOS-Implementierungen |
| `tests/NeroPdf.Tests` | automatische Tests (xUnit) über die Test-PDFs in `TestFiles` |
| `packaging/windows` | MSIX-Manifest |

## Auslieferungswege

- Windows als MSIX-Paket (direkt und über den Windows Store)
- Mac-App im Direktvertrieb, signiert und notarisiert

## Entwicklungsschritte

1. Basisprojekt und Drucker: Avalonia-Basisprojekt, virtueller Drucker unter Windows, reiner Viewer (erledigt)
2. Speichern und Öffnen (inkl. Drag and Drop, sicheres Überschreiben, Passwortdialog)
3. Navigation: seitenweises Scrollen und Seitenübersicht
4. Seitenbearbeitung: Drehen, Löschen, Umsortieren
5. Danach: Mac-Build, Pipeline, Signierung, Store

## Lizenzen

PDFium steht unter der Apache-2.0-Lizenz. Die Lizenztexte von PDFium und seinen Abhängigkeiten werden mit der Anwendung ausgeliefert.

## Weitere Dokumentation

- Ausführliches Konzept und Entwicklungsleitfaden: „Nero PDF – Konzept und Entwicklungsleitfaden“ (Claude Doc)
- Regeln für die Arbeit mit Claude Code: [CLAUDE.md](CLAUDE.md)
