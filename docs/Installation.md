# Nero PDF installieren und ausprobieren

Diese Anleitung beschreibt, wie man den aktuellen Entwicklungsstand von Nero PDF auf einem anderen Windows-Rechner installiert, nutzt und wieder entfernt. Am Ende steht, wie man das Installationspaket selbst baut.

Stand: Entwicklungsschritt 4. Nero PDF zeigt PDFs an, ist virtueller Drucker, kann öffnen und speichern, hat eine Seitenübersicht und bearbeitet Seiten. Danach folgt die Auslieferung (Mac-Version, Pipeline, Store).

## Voraussetzungen

- **Windows 11, Version 24H2 oder neuer, 64 Bit.** Prüfen: Windows-Taste drücken, `winver` eingeben. Dort muss „Version 24H2“ oder höher stehen. Ältere Versionen können das Paket nicht installieren, weil es den virtuellen Drucker von Windows 11 24H2 nutzt.
- **.NET 10 Desktop Runtime (x64).** Download: <https://dotnet.microsoft.com/download/dotnet/10.0>, dort unter „.NET Desktop Runtime 10“ den Installer für „x64“ wählen. Ob sie schon da ist, zeigt in der Eingabeaufforderung der Befehl `dotnet --list-runtimes`. Gesucht ist eine Zeile `Microsoft.NETCore.App 10.…`.
- **Das Installationspaket** `NeroPdf_<Version>_x64.msix`. Es ist mit dem Zertifikat der Nero AG signiert. Ein zusätzliches Zertifikat muss deshalb nicht installiert werden.

## Installieren

1. Die Datei `NeroPdf_<Version>_x64.msix` auf den Rechner kopieren.
2. Doppelklick auf die Datei. Es öffnet sich der App-Installer von Windows.
3. Auf **Installieren** klicken.
4. Nach wenigen Sekunden gibt es einen neuen Drucker **„Nero PDF“**. Zu sehen unter *Einstellungen → Bluetooth und Geräte → Drucker und Scanner*.

Alternativ in PowerShell:

```powershell
Add-AppxPackage -Path .\NeroPdf_0.4.2.0_x64.msix
```

Ist bereits eine ältere Version installiert, wird sie aktualisiert.

## Benutzen

- **Drucken:** In einem beliebigen Programm (Word, Browser, Editor …) drucken und den Drucker **„Nero PDF“** wählen. Nach etwa einer Sekunde öffnet sich Nero PDF mit dem Ergebnis. Frisch gedruckte Dokumente zeigen oben keinen Dateinamen.
- **Ausdruck speichern:** Auf **Speichern** klicken (Strg+S) und einen Ort wählen. Als Name wird der Dokumentname aus dem Druckauftrag vorgeschlagen. Der Ausdruck wird dabei verschoben, nicht kopiert. Wer ein Fenster mit einem ungespeicherten Ausdruck schließt, wird gefragt, ob gespeichert werden soll.
- **Datei öffnen:** Auf **Öffnen** klicken (Strg+O) oder eine PDF-Datei ins Fenster ziehen. Nero PDF erscheint außerdem im Startmenü.
- **Speichern unter:** Bei geöffneten Dateien legt **Speichern unter …** (Strg+Umschalt+S) eine Kopie an einem neuen Ort ab. Das Original bleibt erhalten. Speichern unter dem gleichen Namen am gleichen Ort funktioniert ebenfalls.
- **Passwortgeschützte PDFs:** Beim Öffnen erscheint ein Passwortdialog. Das Passwort wird nirgends gespeichert.
- **Nie gespeicherte Ausdrucke** werden nach 7 Tagen automatisch gelöscht.
- **Seitenübersicht und Blättern:** Links stehen Miniaturen aller Seiten, ein Klick springt zur Seite. Mit Bild auf/ab geht es seitenweise, mit Pos1/Ende zur ersten oder letzten Seite.
- **Seiten bearbeiten:** In der Seitenübersicht Seiten markieren (Klick, Strg+Klick, Umschalt+Klick oder das Auswahlfeld oben rechts an der Miniatur). Die kleine Werkzeugleiste neben der Markierung und das Rechtsklick-Menü drehen, verschieben und löschen. Entf löscht die markierten Seiten. Zum Umsortieren die Seiten mit der Maus an eine andere Stelle ziehen, dort öffnet sich eine Lücke. Alle Änderungen betreffen zunächst nur das geöffnete Dokument. Die Datei ändert sich erst beim Speichern.
- **Seitenübersicht einklappen:** Der Pfeil oben in der Leiste klappt die Übersicht ein und aus. Die Produktkacheln bleiben dabei als Icons sichtbar.
- **Seitenübersicht breiter oder schmaler:** Den rechten Rand der Seitenübersicht mit der Maus ziehen. Die Vorschauen wachsen mit, die Breite wird gemerkt. Schmaler als die Standardbreite geht es nicht.
- **Hell und dunkel:** Das Symbol unten rechts schaltet um. Die Wahl wird gespeichert.
- **Mehrere Drucke gleichzeitig** öffnen jeweils ein eigenes Fenster.

## Entfernen

1. *Einstellungen → Apps → Installierte Apps → Nero PDF → Deinstallieren.* Der Drucker verschwindet dabei. Das ist auf einem Testrechner geprüft: Paket, Drucker und Druckerport sind danach vollständig entfernt.
2. Für Tests oder nach einer abgebrochenen Installation entfernt `build\uninstall-windows.ps1` Nero PDF samt Drucker vollständig (Ausgaben auf Englisch, als Hilfswerkzeug gedacht). Reste eines Druckerports entfernt es, wenn es „als Administrator“ läuft.
3. Falls Windows beim Deinstallieren doch einen Druckerport-Eintrag in der Registry zurücklässt: Er stört nicht. Wer ihn entfernen will, löscht als Administrator unter `HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Print\Monitors\Virtual Port Monitor\Ports` den Schlüssel, der mit `NeroPdf_` beginnt, und startet den Rechner neu.

## Wenn etwas nicht funktioniert

Nero PDF schreibt ein Protokoll (immer auf Englisch, ohne Dokumentinhalte und Passwörter):

```
%LOCALAPPDATA%\Packages\NeroPdf_9kh40daskfajr\LocalState\Logs
```

Den Ordner in den Explorer kopieren und die neueste Datei `NeroPdf_<Datum>.log` mitschicken. Für ein ausführlicheres Protokoll Nero PDF mit dem Parameter `--verbose` starten.

Häufige Ursachen:

| Problem | Ursache und Lösung |
| --- | --- |
| Installation bricht mit Hinweis auf die Windows-Version ab | Windows ist älter als 11 24H2. Windows aktualisieren. |
| Nero PDF startet nicht oder schließt sich sofort | Die .NET 10 Runtime fehlt. Installieren, siehe Voraussetzungen. |
| Drucker „Nero PDF“ fehlt nach der Installation | Einige Sekunden warten oder den Rechner neu starten. Danach das Protokoll prüfen. |

## Paket selbst bauen (Entwickler)

Voraussetzungen: .NET 10 SDK, Windows SDK (für `makeappx`, `makepri`, `signtool`) und das Code-Signing-Zertifikat „Nero AG“ im Zertifikatsspeicher des Benutzers. Zertifikate liegen nie im Repository.

```powershell
.\build\package-windows.ps1 -CertificateName "Nero AG" -Version 0.1.2.0
```

Das Paket liegt danach unter `artifacts\package\`. Mit `-Install` wird es gleich auf dem eigenen Rechner installiert. Die Versionsnummer muss bei jeder neuen Auslieferung steigen.

## Lizenzen

Nero PDF bringt die Lizenztexte, Copyright- und NOTICE-Dateien aller mitgelieferten Fremdkomponenten (PDFium und seine Bibliotheken, Avalonia, SkiaSharp, HarfBuzz und weitere) im Installationsordner unter `Licenses` mit. Die Übersicht steht in `Licenses\THIRD-PARTY-NOTICES.txt`, dort sind auch Änderungen an übernommenen Dateien vermerkt. Wird ein Paket aktualisiert, sammelt `build\update-licenses.ps1` die Texte neu ein. Ein Test prüft, dass die PDFium-Lizenzen zur verwendeten Version passen.
