# Änderungen

## 1.1 (2026-11-14)
- **Neu:** Gil × 4 (Option `--gil-mul`, höchstens 65.535 Gil pro Gegner)
- **Neu:** AP × 4 (Option `--ap-mul`, höchstens 255 AP pro Kampf)
- Erkennt jetzt jede bereits veränderte europäische ROM (nicht nur bekannte Patch-Stände) und verweigert sie
- Ausgabedatei heißt jetzt `Final Fantasy VI Advance (Europe) (De) [HP x0.25, EXP x4, Gil x4, AP x4].gba`
- Ergebnis-SHA-1 mit Standardwerten: `42a61a590884f9171ac43e307d25dd754129bfa5`
- Mit `--gil-mul 1 --ap-mul 1` entsteht exakt das Ergebnis von Version 1.0

## 1.0 (2026-11-14)
- Gegner-HP ÷ 4, EXP × 4
- HP-Schwellen in Boss-KI-Skripten werden mitskaliert
- Ergebnis-SHA-1 mit Standardwerten: `ea347abd3c0e86133cacf1d81fe99f83cb270720`
