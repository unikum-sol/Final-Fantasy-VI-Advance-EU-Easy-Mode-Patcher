// FF6A-Patcher: Final Fantasy VI Advance (Europe) – Gegner-HP / 4, EXP/Gil/AP x 4.
// Gleiche Logik wie python/ff6a_patch.py.
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace FF6APatcher;

internal static class Patch
{
    public const string EuSha1 = "c5b27ed0870ea64e6a001c5bc367f76571ace973";

    // Monster-Werte: 423 Einträge zu je 32 Byte
    const int StatsOffset = 0x737A3E;
    const int MonsterCount = 423;
    const int RecSize = 32;
    const int OffHp = 0x08;  // u16
    const int OffExp = 0x0C; // u16
    const int OffGil = 0x0E; // u16

    // AP pro Kampfformation: 1 Byte je Formation 0-511 (höhere Formationen geben keine AP)
    const int ApTable = 0x79F400;
    const int ApCount = 512;

    // KI-Skripte: Tabelle mit 498 u16-Offsets, Skripte direkt danach
    const int AiTable = 0x727FF6;
    const int AiCount = 498;
    const int AiBase = AiTable + 2 * AiCount;

    static int CmdLen(byte c) => c switch
    {
        0xF1 or 0xF7 => 2,
        0xF3 or 0xF8 or 0xFB => 3,
        0xF0 or 0xF2 or 0xF4 or 0xF5 or 0xF6 or 0xF9 or 0xFA or 0xFC => 4,
        _ => 1,
    };

    static int DivRound(int v, int d) => (v + d / 2) / d;

    public static string Sha1(byte[] data) => Convert.ToHexString(SHA1.HashData(data)).ToLowerInvariant();

    // Spielkennung im ROM-Header: "BZ6P" = FF6 Advance Europa
    public static bool IsFf6aEurope(byte[] rom) =>
        rom.Length > 0xB0 && Encoding.ASCII.GetString(rom, 0xAC, 4) == "BZ6P";

    public record Result(byte[] Rom, int Hp, int Exp, int Gil, int Ap, int Thresholds);

    public static Result Apply(byte[] rom, int hpDiv, int expMul, int gilMul, int apMul)
    {
        if (Sha1(rom) != EuSha1)
            throw new InvalidDataException(IsFf6aEurope(rom)
                ? "Diese ROM ist bereits verändert (z. B. schon gepatcht). Bitte die Original-ROM verwenden."
                : "Falsche ROM. Erwartet wird die unveränderte europäische Version\n" +
                  "\"Final Fantasy VI Advance (Europe) (En,Fr,De,Es,It)\".");

        // Plausibilität: Monster 0 = Wache (HP 40, MP 15, EXP 48, Gil 48, Level 5)
        if (U16(rom, StatsOffset + 8) != 40 || U16(rom, StatsOffset + 12) != 48 || rom[StatsOffset + 0x10] != 5)
            throw new InvalidDataException("Monster-Tabelle nicht an erwarteter Stelle.");
        // AP-Tabelle: Formation 210 (Intangir) und 221 (Kaktor) geben je 10 AP
        if (rom[ApTable + 210] != 10 || rom[ApTable + 221] != 10)
            throw new InvalidDataException("AP-Tabelle nicht an erwarteter Stelle.");

        var o = (byte[])rom.Clone();
        int hpC = 0, expC = 0, gilC = 0, apC = 0, thrC = 0;

        for (int i = 0; i < MonsterCount; i++)
        {
            int p = StatsOffset + i * RecSize;
            int hp = U16(rom, p + OffHp), exp = U16(rom, p + OffExp), gil = U16(rom, p + OffGil);
            int nhp = hp > 0 ? Math.Max(1, DivRound(hp, hpDiv)) : 0;
            int nexp = (int)Math.Min(0xFFFFL, (long)exp * expMul);
            int ngil = (int)Math.Min(0xFFFFL, (long)gil * gilMul);
            BinaryPrimitives.WriteUInt16LittleEndian(o.AsSpan(p + OffHp), (ushort)nhp);
            BinaryPrimitives.WriteUInt16LittleEndian(o.AsSpan(p + OffExp), (ushort)nexp);
            BinaryPrimitives.WriteUInt16LittleEndian(o.AsSpan(p + OffGil), (ushort)ngil);
            if (nhp != hp) hpC++;
            if (nexp != exp) expC++;
            if (ngil != gil) gilC++;
        }

        for (int f = 0; f < ApCount; f++)
        {
            int ap = rom[ApTable + f];
            int nap = Math.Min(0xFF, ap * apMul);
            o[ApTable + f] = (byte)nap;
            if (nap != ap) apC++;
        }

        var seen = new HashSet<int>();
        for (int i = 0; i < MonsterCount; i++)
        {
            int p = AiBase + U16(rom, AiTable + 2 * i);
            int ends = 0, steps = 0;
            while (ends < 2) // Hauptteil + Konterteil, beide enden mit 0xFF
            {
                byte c = rom[p];
                // FC 06 tt xx = "wenn HP von Ziel tt < xx * k"
                if (c == 0xFC && rom[p + 1] == 0x06 && seen.Add(p))
                {
                    o[p + 3] = (byte)Math.Max(1, DivRound(rom[p + 3], hpDiv));
                    thrC++;
                }
                if (c == 0xFF) ends++;
                p += CmdLen(c);
                if (++steps > 3000) throw new InvalidDataException($"KI-Skript von Monster {i} unlesbar.");
            }
        }
        return new Result(o, hpC, expC, gilC, apC, thrC);
    }

    static int U16(byte[] b, int p) => BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(p));
}

internal static class Program
{
    const string Version = "1.1";

    // Programm läuft in eigenem Konsolenfenster (Windows-Doppelklick / Drag & Drop)?
    static bool ownWindow;

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    static extern uint GetConsoleProcessList(uint[] list, uint count);

    static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }
        if (OperatingSystem.IsWindows())
        {
            try { ownWindow = GetConsoleProcessList(new uint[4], 4) <= 1; } catch { }
        }
        try { return Run(args); }
        catch (Exception e)
        {
            Console.Error.WriteLine($"\nFehler: {e.Message}");
            Pause();
            return 1;
        }
    }

    static int Run(string[] args)
    {
        string? input = null, output = null;
        int hpDiv = 4, expMul = 4, gilMul = 4, apMul = 4;
        bool force = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-h": case "--help": case "/?":
                    PrintHelp(); return 0;
                case "-o": case "--output": output = Next(args, ref i); break;
                case "--hp-div": hpDiv = PosInt(Next(args, ref i), "--hp-div"); break;
                case "--exp-mul": expMul = PosInt(Next(args, ref i), "--exp-mul"); break;
                case "--gil-mul": gilMul = PosInt(Next(args, ref i), "--gil-mul"); break;
                case "--ap-mul": apMul = PosInt(Next(args, ref i), "--ap-mul"); break;
                case "-f": case "--force": force = true; break;
                default:
                    if (args[i].StartsWith('-')) throw new ArgumentException($"Unbekannte Option: {args[i]}");
                    input = args[i]; break;
            }
        }

        Console.WriteLine($"FF6A-Patcher {Version} – Gegner-HP / {hpDiv}, EXP x {expMul}, Gil x {gilMul}, AP x {apMul}\n");

        input ??= FindRom() ?? AskPath();
        input = Path.GetFullPath(input.Trim().Trim('"', '\''));
        if (!File.Exists(input)) throw new FileNotFoundException($"Datei nicht gefunden: {input}");

        byte[] rom = File.ReadAllBytes(input);
        Console.WriteLine($"ROM:   {input}");
        var r = Patch.Apply(rom, hpDiv, expMul, gilMul, apMul);

        string hpTag = hpDiv == 4 ? "HP x0.25" : $"HP div{hpDiv}";
        string tag = $"{hpTag}, EXP x{expMul}, Gil x{gilMul}, AP x{apMul}";
        output = Path.GetFullPath(output ?? Path.Combine(Path.GetDirectoryName(input)!,
            $"Final Fantasy VI Advance (Europe) (De) [{tag}].gba"));

        if (File.Exists(output) && !force)
        {
            if (Console.IsInputRedirected)
                throw new IOException($"Ausgabedatei existiert bereits (mit -f überschreiben): {output}");
            Console.Write($"\n\"{Path.GetFileName(output)}\" existiert bereits. Überschreiben? [j/N] ");
            var a = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (a is not ("j" or "ja" or "y" or "yes")) { Console.WriteLine("Abgebrochen."); Pause(); return 2; }
        }

        File.WriteAllBytes(output, r.Rom);
        Console.WriteLine($"\nGegner-HP geändert: {r.Hp}");
        Console.WriteLine($"EXP geändert:       {r.Exp}");
        Console.WriteLine($"Gil geändert:       {r.Gil}");
        Console.WriteLine($"AP geändert:        {r.Ap} Kampfformationen");
        Console.WriteLine($"KI-HP-Schwellen:    {r.Thresholds}");
        Console.WriteLine($"SHA-1:              {Patch.Sha1(r.Rom)}");
        Console.WriteLine($"\nFertig: {output}");
        Pause();
        return 0;
    }

    // Sucht die Original-ROM neben dem Programm und im aktuellen Ordner.
    static string? FindRom()
    {
        var dirs = new[] { AppContext.BaseDirectory, Environment.CurrentDirectory }
            .Select(d => Path.GetFullPath(d)).Distinct();
        foreach (var dir in dirs)
            foreach (var f in Directory.EnumerateFiles(dir, "*.gba"))
            {
                var info = new FileInfo(f);
                if (info.Length == 16 * 1024 * 1024 && Patch.Sha1(File.ReadAllBytes(f)) == Patch.EuSha1)
                    return f;
            }
        return null;
    }

    static string AskPath()
    {
        Console.WriteLine("Keine passende ROM neben dem Programm gefunden.");
        Console.Write("Pfad zur Original-ROM eingeben (oder Datei ins Fenster ziehen): ");
        var s = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(s)) throw new ArgumentException("Kein Pfad angegeben.");
        return s;
    }

    static string Next(string[] a, ref int i) =>
        ++i < a.Length ? a[i] : throw new ArgumentException($"Wert fehlt für {a[i - 1]}");

    static int PosInt(string s, string name) =>
        int.TryParse(s, out int v) && v >= 1 && v <= 255 ? v : throw new ArgumentException($"{name} muss 1–255 sein.");

    static void Pause()
    {
        if (!ownWindow) return;
        Console.Write("\nEnter drücken zum Beenden …");
        try { Console.ReadLine(); } catch { }
    }

    static void PrintHelp() => Console.WriteLine($"""
        FF6A-Patcher {Version} – Final Fantasy VI Advance (Europe)

        Aufruf:
          FF6A-Patcher [ROM.gba] [Optionen]

        Ohne Argumente wird die Original-ROM neben dem Programm bzw. im aktuellen
        Ordner gesucht. Unter Windows kann die ROM auch auf die .exe gezogen werden.

        Optionen:
          -o, --output DATEI   Ausgabedatei (Standard: neben der ROM)
          --hp-div N           Gegner-HP teilen durch N (Standard 4)
          --exp-mul N          EXP multiplizieren mit N (Standard 4, max. 65535 EXP)
          --gil-mul N          Gil multiplizieren mit N (Standard 4, max. 65535 Gil)
          --ap-mul N           AP multiplizieren mit N (Standard 4, max. 255 AP)
          -f, --force          Vorhandene Ausgabedatei überschreiben
          -h, --help           Diese Hilfe

        Benötigt: Final Fantasy VI Advance (Europe) (En,Fr,De,Es,It)
                  SHA-1 c5b27ed0870ea64e6a001c5bc367f76571ace973
        """);
}
