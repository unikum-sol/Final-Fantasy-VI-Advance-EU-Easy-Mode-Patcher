# Final Fantasy VI Advance (EU) - Easy Mode Patcher

Programmname: `FF6A-Patcher`

Patch für **Final Fantasy VI Advance (Europe) (En,Fr,De,Es,It)**:

- Gegner-HP ÷ 4 (mindestens 1 HP)
- EXP × 4 (höchstens 65.535)
- Gil × 4 (höchstens 65.535)
- AP × 4 (höchstens 255)
- HP-Schwellen in Boss-KI-Skripten werden mitskaliert, damit Phasenwechsel und Rückzüge wie im Original ablaufen

Benötigt wird die unveränderte Original-ROM (SHA-1 `c5b27ed0870ea64e6a001c5bc367f76571ace973`).
Die ROM selbst ist **nicht** enthalten. Die Original-ROM wird nie verändert, das Ergebnis landet als neue Datei daneben:
`Final Fantasy VI Advance (Europe) (De) [HP x0.25, EXP x4, Gil x4, AP x4].gba`

## Fertige Programme

Die fertigen Programme gibt es als ZIP unter **Releases**:

| System | Paket |
|---|---|
| Windows (x64) | `FF6A-Patcher-<Version>-windows-x64.zip` |
| macOS (Apple Silicon) | `FF6A-Patcher-<Version>-macos-arm64.zip` |
| Linux (x64) | `FF6A-Patcher-<Version>-linux-x64.zip` |
| Python-Skript | `FF6A-Patcher-<Version>-python.zip` |

Es muss nichts zusätzlich installiert werden. Die Prüfsummen stehen in `SHA256SUMS.txt`.
Wer selbst baut (siehe unten), findet die Programme danach in `dist/`.

### Windows
- ROM auf `FF6A-Patcher.exe` ziehen, **oder**
- Patcher in den Ordner mit der ROM legen und doppelklicken (er findet die ROM selbst).

### macOS
```bash
chmod +x FF6A-Patcher
xattr -d com.apple.quarantine FF6A-Patcher   # nur nötig, wenn die Datei heruntergeladen wurde
./FF6A-Patcher "Final Fantasy VI Advance (Europe) (En,Fr,De,Es,It).gba"
```

### Linux
```bash
chmod +x FF6A-Patcher
./FF6A-Patcher "Final Fantasy VI Advance (Europe) (En,Fr,De,Es,It).gba"
```

### Optionen
```
FF6A-Patcher [ROM.gba] [-o AUSGABE.gba] [--hp-div N] [--exp-mul N] [--gil-mul N] [--ap-mul N] [-f]
  --hp-div N    Gegner-HP durch N teilen (Standard 4)
  --exp-mul N   EXP mit N multiplizieren (Standard 4)
  --gil-mul N   Gil mit N multiplizieren (Standard 4)
  --ap-mul N    AP mit N multiplizieren (Standard 4)
  -f            vorhandene Ausgabedatei überschreiben
```
Mit `--gil-mul 1 --ap-mul 1` entsteht exakt die ältere Version 1.0 (nur HP und EXP).

## Python-Skript

`python/ff6a_patch.py` macht dasselbe (Python 3.9+, keine Zusatzpakete) und hat dieselben Optionen:
```bash
python ff6a_patch.py "Final Fantasy VI Advance (Europe) (En,Fr,De,Es,It).gba"
```

## Selbst bauen

Voraussetzung: .NET SDK 10. Dann `build.ps1` (Windows) oder `build.sh` (macOS/Linux) ausführen.
Alle drei Plattformen lassen sich von jedem System aus bauen.

## Technische Details

| Daten | ROM-Offset | Format |
|---|---|---|
| Monster-Werte | `0x737A3E` | 423 × 32 Byte; HP = u16 @ +0x08, EXP = u16 @ +0x0C, Gil = u16 @ +0x0E |
| AP pro Kampfformation | `0x79F400` | 512 × 1 Byte (Formationen 0–511; das Spiel gibt für höhere Formationen keine AP) |
| KI-Skript-Tabelle | `0x727FF6` | 498 × u16-Offset, Skripte ab `0x7283DA` |
| KI-HP-Bedingung | in den Skripten | `FC 06 tt xx` → `xx` wird ebenfalls ÷ 4 gerechnet (31 Stellen) |

Ergebnis mit Standardwerten: SHA-1 `42a61a590884f9171ac43e307d25dd754129bfa5`.

Die AP gibt es pro Kampf, nicht pro Monster. Dieselben Monster können in verschiedenen Gegenden daher unterschiedlich viele AP geben.
