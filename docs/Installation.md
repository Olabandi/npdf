# Nero PDF installieren und ausprobieren

Diese Anleitung beschreibt, wie man den aktuellen Entwicklungsstand von Nero PDF auf einem anderen Windows-Rechner installiert, nutzt und wieder entfernt. Am Ende steht, wie man das Installationspaket selbst baut.

Stand: Entwicklungsschritt 1. Nero PDF ist ein reiner Viewer mit virtuellem Drucker. Öffnen-Dialog, Drag and Drop und Speichern kommen mit Schritt 2.

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
Add-AppxPackage -Path .\NeroPdf_0.1.2.0_x64.msix
```

Ist bereits eine ältere Version installiert, wird sie aktualisiert.

## Benutzen

- **Drucken:** In einem beliebigen Programm (Word, Browser, Editor …) drucken und den Drucker **„Nero PDF“** wählen. Nach etwa einer Sekunde öffnet sich Nero PDF mit dem Ergebnis. Frisch gedruckte Dokumente zeigen oben keinen Dateinamen.
- **Datei ansehen:** In Schritt 1 nur über die Kommandozeile, z. B. `NeroPdf.exe "C:\Pfad\Datei.pdf"`. Nero PDF erscheint außerdem im Startmenü und startet dort ohne Dokument.
- **Hell und dunkel:** Das Symbol unten rechts schaltet um. Die Wahl wird gespeichert.
- **Mehrere Drucke gleichzeitig** öffnen jeweils ein eigenes Fenster.

## Entfernen

1. *Einstellungen → Apps → Installierte Apps → Nero PDF → Deinstallieren.* Der Drucker verschwindet dabei.
2. Optional: Windows lässt beim Deinstallieren einen Druckerport-Eintrag in der Registry zurück. Er stört nicht. Wer ihn entfernen will, löscht als Administrator unter `HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Print\Monitors\Virtual Port Monitor\Ports` den Schlüssel, der mit `NeroPdf_` beginnt, und startet den Rechner neu.

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
