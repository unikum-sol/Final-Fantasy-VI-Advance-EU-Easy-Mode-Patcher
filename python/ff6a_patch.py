#!/usr/bin/env python3
"""
FF6 Advance (Europe) Patch: weniger Gegner-HP, mehr EXP, Gil und AP.

Standard: Gegner-HP / 4, EXP x 4, Gil x 4, AP x 4. HP-Schwellen in Boss-KI-Skripten
(Bedingung "wenn eigene HP < X") werden im gleichen Verhältnis mitskaliert,
damit Phasenwechsel/Rückzüge nicht sofort beim Kampfstart auslösen.

Nur für: Final Fantasy VI Advance (Europe) (En,Fr,De,Es,It)
         SHA-1 c5b27ed0870ea64e6a001c5bc367f76571ace973

Aufruf:
    python ff6a_patch.py ROM.gba [-o AUSGABE.gba] [--hp-div 4] [--exp-mul 4]
                         [--gil-mul 4] [--ap-mul 4]
"""
import argparse
import hashlib
import os
import struct
import sys

EU_SHA1 = "c5b27ed0870ea64e6a001c5bc367f76571ace973"

# Monster-Werte: 423 Einträge zu je 32 Byte
STATS_OFFSET = 0x737A3E
MONSTER_COUNT = 423
REC_SIZE = 32
OFF_HP = 0x08   # u16
OFF_EXP = 0x0C  # u16
OFF_GIL = 0x0E  # u16

# AP pro Kampfformation: 1 Byte je Formation 0-511 (höhere Formationen geben keine AP)
AP_TABLE = 0x79F400
AP_COUNT = 512

# KI-Skripte: Tabelle mit 498 u16-Offsets, Skripte direkt danach
AI_TABLE = 0x727FF6
AI_COUNT = 498
AI_BASE = AI_TABLE + 2 * AI_COUNT

# Länge der KI-Befehle (alles unter 0xF0 = 1 Byte)
AI_CMD_LEN = {0xF0: 4, 0xF1: 2, 0xF2: 4, 0xF3: 3, 0xF4: 4, 0xF5: 4, 0xF6: 4, 0xF7: 2,
              0xF8: 3, 0xF9: 4, 0xFA: 4, 0xFB: 3, 0xFC: 4, 0xFD: 1, 0xFE: 1, 0xFF: 1}


def div_round(value, divisor):
    return (value + divisor // 2) // divisor


def patch(rom: bytes, hp_div: int = 4, exp_mul: int = 4,
          gil_mul: int = 4, ap_mul: int = 4) -> tuple[bytearray, dict]:
    if hashlib.sha1(rom).hexdigest() != EU_SHA1:
        if rom[0xAC:0xB0] == b"BZ6P":  # Spielkennung FF6 Advance Europa
            raise ValueError("Diese ROM ist bereits verändert (z. B. schon gepatcht). "
                             "Bitte die Original-ROM verwenden.")
        raise ValueError("Falsche ROM: erwartet wird die unveränderte europäische Version "
                         "(Final Fantasy VI Advance (Europe) (En,Fr,De,Es,It)).")
    # Plausibilitätsprüfung: Monster 0 = Wache (HP 40, MP 15, EXP 48, Gil 48, Level 5)
    assert struct.unpack_from("<HHHH", rom, STATS_OFFSET + 8) == (40, 15, 48, 48)
    assert rom[STATS_OFFSET + 0x10] == 5
    # AP-Tabelle: Formation 210 (Intangir) und 221 (Kaktor) geben je 10 AP
    assert rom[AP_TABLE + 210] == 10 and rom[AP_TABLE + 221] == 10

    out = bytearray(rom)
    stats = {"hp": 0, "exp": 0, "gil": 0, "ap": 0, "thresholds": 0}

    for i in range(MONSTER_COUNT):
        p = STATS_OFFSET + i * REC_SIZE
        hp = struct.unpack_from("<H", rom, p + OFF_HP)[0]
        exp = struct.unpack_from("<H", rom, p + OFF_EXP)[0]
        gil = struct.unpack_from("<H", rom, p + OFF_GIL)[0]
        new_hp = max(1, div_round(hp, hp_div)) if hp > 0 else 0
        new_exp = min(0xFFFF, exp * exp_mul)
        new_gil = min(0xFFFF, gil * gil_mul)
        struct.pack_into("<H", out, p + OFF_HP, new_hp)
        struct.pack_into("<H", out, p + OFF_EXP, new_exp)
        struct.pack_into("<H", out, p + OFF_GIL, new_gil)
        stats["hp"] += new_hp != hp
        stats["exp"] += new_exp != exp
        stats["gil"] += new_gil != gil

    for f in range(AP_COUNT):
        ap = rom[AP_TABLE + f]
        new_ap = min(0xFF, ap * ap_mul)
        out[AP_TABLE + f] = new_ap
        stats["ap"] += new_ap != ap

    offsets = struct.unpack_from(f"<{AI_COUNT}H", rom, AI_TABLE)
    seen = set()
    for i in range(MONSTER_COUNT):
        p = AI_BASE + offsets[i]
        ends = steps = 0
        while ends < 2:  # Skript = Hauptteil + Konterteil, beide enden mit 0xFF
            cmd = rom[p]
            # FC 06 tt xx = "wenn HP von Ziel tt < xx * k"
            if cmd == 0xFC and rom[p + 1] == 0x06 and p not in seen:
                seen.add(p)
                out[p + 3] = max(1, div_round(rom[p + 3], hp_div))
                stats["thresholds"] += 1
            if cmd == 0xFF:
                ends += 1
            p += AI_CMD_LEN.get(cmd, 1)
            steps += 1
            if steps > 3000:
                raise RuntimeError(f"KI-Skript von Monster {i} konnte nicht gelesen werden")
    return out, stats


def main():
    ap = argparse.ArgumentParser(description="FF6 Advance (EU): Gegner-HP teilen, EXP/Gil/AP vervielfachen")
    ap.add_argument("rom", help="Original-ROM (Europe, En/Fr/De/Es/It)")
    ap.add_argument("-o", "--output", help="Ausgabedatei (Standard: neben der ROM)")
    ap.add_argument("--hp-div", type=int, default=4, help="HP-Teiler (Standard 4)")
    ap.add_argument("--exp-mul", type=int, default=4, help="EXP-Faktor (Standard 4)")
    ap.add_argument("--gil-mul", type=int, default=4, help="Gil-Faktor (Standard 4)")
    ap.add_argument("--ap-mul", type=int, default=4, help="AP-Faktor (Standard 4)")
    a = ap.parse_args()
    for name in ("hp_div", "exp_mul", "gil_mul", "ap_mul"):
        if not 1 <= getattr(a, name) <= 255:
            ap.error(f"--{name.replace('_', '-')} muss zwischen 1 und 255 liegen")

    rom = open(a.rom, "rb").read()
    try:
        out, st = patch(rom, a.hp_div, a.exp_mul, a.gil_mul, a.ap_mul)
    except ValueError as e:
        sys.exit(f"Fehler: {e}")

    hp_tag = "HP x0.25" if a.hp_div == 4 else f"HP div{a.hp_div}"
    tag = f"{hp_tag}, EXP x{a.exp_mul}, Gil x{a.gil_mul}, AP x{a.ap_mul}"
    dst = a.output or os.path.join(os.path.dirname(os.path.abspath(a.rom)),
                                   f"Final Fantasy VI Advance (Europe) (De) [{tag}].gba")
    with open(dst, "wb") as f:
        f.write(out)
    print(f"HP geändert: {st['hp']}, EXP geändert: {st['exp']}, Gil geändert: {st['gil']}, "
          f"AP geändert: {st['ap']}, KI-Schwellen: {st['thresholds']}")
    print(f"SHA-1: {hashlib.sha1(out).hexdigest()}")
    print(f"Geschrieben: {dst}")


if __name__ == "__main__":
    main()
