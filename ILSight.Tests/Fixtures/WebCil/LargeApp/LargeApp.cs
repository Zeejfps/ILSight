// The source of LargeApp.wasm, a WebCIL fixture over 64 KB: built with LargeApp.csproj beside it (Release), then
// wrapped in WebAssembly by WebcilConverter (Microsoft.NET.WebAssembly.Webcil, from the SDK's WebAssembly pack 10.0.6).
// Its late parts (the metadata heaps) lie past the file's first 64 KB, so a read of them after the mapped file is
// released fails the test run.
namespace LargeApp;

/// <summary>Part 0 of the fixture.</summary>
public sealed class Part000
{
    private readonly string _name = "Part 000: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 000)";
        }
        Count++;
        return text;
    }
    public int Add000(int left, int right)
    {
        return left + right + 0;
    }
}

/// <summary>Part 1 of the fixture.</summary>
public sealed class Part001
{
    private readonly string _name = "Part 001: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 001)";
        }
        Count++;
        return text;
    }
    public int Add001(int left, int right)
    {
        return left + right + 1;
    }
}

/// <summary>Part 2 of the fixture.</summary>
public sealed class Part002
{
    private readonly string _name = "Part 002: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 002)";
        }
        Count++;
        return text;
    }
    public int Add002(int left, int right)
    {
        return left + right + 2;
    }
}

/// <summary>Part 3 of the fixture.</summary>
public sealed class Part003
{
    private readonly string _name = "Part 003: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 003)";
        }
        Count++;
        return text;
    }
    public int Add003(int left, int right)
    {
        return left + right + 3;
    }
}

/// <summary>Part 4 of the fixture.</summary>
public sealed class Part004
{
    private readonly string _name = "Part 004: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 004)";
        }
        Count++;
        return text;
    }
    public int Add004(int left, int right)
    {
        return left + right + 4;
    }
}

/// <summary>Part 5 of the fixture.</summary>
public sealed class Part005
{
    private readonly string _name = "Part 005: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 005)";
        }
        Count++;
        return text;
    }
    public int Add005(int left, int right)
    {
        return left + right + 5;
    }
}

/// <summary>Part 6 of the fixture.</summary>
public sealed class Part006
{
    private readonly string _name = "Part 006: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 006)";
        }
        Count++;
        return text;
    }
    public int Add006(int left, int right)
    {
        return left + right + 6;
    }
}

/// <summary>Part 7 of the fixture.</summary>
public sealed class Part007
{
    private readonly string _name = "Part 007: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 007)";
        }
        Count++;
        return text;
    }
    public int Add007(int left, int right)
    {
        return left + right + 7;
    }
}

/// <summary>Part 8 of the fixture.</summary>
public sealed class Part008
{
    private readonly string _name = "Part 008: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 008)";
        }
        Count++;
        return text;
    }
    public int Add008(int left, int right)
    {
        return left + right + 8;
    }
}

/// <summary>Part 9 of the fixture.</summary>
public sealed class Part009
{
    private readonly string _name = "Part 009: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 009)";
        }
        Count++;
        return text;
    }
    public int Add009(int left, int right)
    {
        return left + right + 9;
    }
}

/// <summary>Part 10 of the fixture.</summary>
public sealed class Part010
{
    private readonly string _name = "Part 010: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 010)";
        }
        Count++;
        return text;
    }
    public int Add010(int left, int right)
    {
        return left + right + 10;
    }
}

/// <summary>Part 11 of the fixture.</summary>
public sealed class Part011
{
    private readonly string _name = "Part 011: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 011)";
        }
        Count++;
        return text;
    }
    public int Add011(int left, int right)
    {
        return left + right + 11;
    }
}

/// <summary>Part 12 of the fixture.</summary>
public sealed class Part012
{
    private readonly string _name = "Part 012: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 012)";
        }
        Count++;
        return text;
    }
    public int Add012(int left, int right)
    {
        return left + right + 12;
    }
}

/// <summary>Part 13 of the fixture.</summary>
public sealed class Part013
{
    private readonly string _name = "Part 013: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 013)";
        }
        Count++;
        return text;
    }
    public int Add013(int left, int right)
    {
        return left + right + 13;
    }
}

/// <summary>Part 14 of the fixture.</summary>
public sealed class Part014
{
    private readonly string _name = "Part 014: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 014)";
        }
        Count++;
        return text;
    }
    public int Add014(int left, int right)
    {
        return left + right + 14;
    }
}

/// <summary>Part 15 of the fixture.</summary>
public sealed class Part015
{
    private readonly string _name = "Part 015: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 015)";
        }
        Count++;
        return text;
    }
    public int Add015(int left, int right)
    {
        return left + right + 15;
    }
}

/// <summary>Part 16 of the fixture.</summary>
public sealed class Part016
{
    private readonly string _name = "Part 016: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 016)";
        }
        Count++;
        return text;
    }
    public int Add016(int left, int right)
    {
        return left + right + 16;
    }
}

/// <summary>Part 17 of the fixture.</summary>
public sealed class Part017
{
    private readonly string _name = "Part 017: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 017)";
        }
        Count++;
        return text;
    }
    public int Add017(int left, int right)
    {
        return left + right + 17;
    }
}

/// <summary>Part 18 of the fixture.</summary>
public sealed class Part018
{
    private readonly string _name = "Part 018: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 018)";
        }
        Count++;
        return text;
    }
    public int Add018(int left, int right)
    {
        return left + right + 18;
    }
}

/// <summary>Part 19 of the fixture.</summary>
public sealed class Part019
{
    private readonly string _name = "Part 019: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 019)";
        }
        Count++;
        return text;
    }
    public int Add019(int left, int right)
    {
        return left + right + 19;
    }
}

/// <summary>Part 20 of the fixture.</summary>
public sealed class Part020
{
    private readonly string _name = "Part 020: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 020)";
        }
        Count++;
        return text;
    }
    public int Add020(int left, int right)
    {
        return left + right + 20;
    }
}

/// <summary>Part 21 of the fixture.</summary>
public sealed class Part021
{
    private readonly string _name = "Part 021: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 021)";
        }
        Count++;
        return text;
    }
    public int Add021(int left, int right)
    {
        return left + right + 21;
    }
}

/// <summary>Part 22 of the fixture.</summary>
public sealed class Part022
{
    private readonly string _name = "Part 022: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 022)";
        }
        Count++;
        return text;
    }
    public int Add022(int left, int right)
    {
        return left + right + 22;
    }
}

/// <summary>Part 23 of the fixture.</summary>
public sealed class Part023
{
    private readonly string _name = "Part 023: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 023)";
        }
        Count++;
        return text;
    }
    public int Add023(int left, int right)
    {
        return left + right + 23;
    }
}

/// <summary>Part 24 of the fixture.</summary>
public sealed class Part024
{
    private readonly string _name = "Part 024: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 024)";
        }
        Count++;
        return text;
    }
    public int Add024(int left, int right)
    {
        return left + right + 24;
    }
}

/// <summary>Part 25 of the fixture.</summary>
public sealed class Part025
{
    private readonly string _name = "Part 025: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 025)";
        }
        Count++;
        return text;
    }
    public int Add025(int left, int right)
    {
        return left + right + 25;
    }
}

/// <summary>Part 26 of the fixture.</summary>
public sealed class Part026
{
    private readonly string _name = "Part 026: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 026)";
        }
        Count++;
        return text;
    }
    public int Add026(int left, int right)
    {
        return left + right + 26;
    }
}

/// <summary>Part 27 of the fixture.</summary>
public sealed class Part027
{
    private readonly string _name = "Part 027: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 027)";
        }
        Count++;
        return text;
    }
    public int Add027(int left, int right)
    {
        return left + right + 27;
    }
}

/// <summary>Part 28 of the fixture.</summary>
public sealed class Part028
{
    private readonly string _name = "Part 028: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 028)";
        }
        Count++;
        return text;
    }
    public int Add028(int left, int right)
    {
        return left + right + 28;
    }
}

/// <summary>Part 29 of the fixture.</summary>
public sealed class Part029
{
    private readonly string _name = "Part 029: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 029)";
        }
        Count++;
        return text;
    }
    public int Add029(int left, int right)
    {
        return left + right + 29;
    }
}

/// <summary>Part 30 of the fixture.</summary>
public sealed class Part030
{
    private readonly string _name = "Part 030: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 030)";
        }
        Count++;
        return text;
    }
    public int Add030(int left, int right)
    {
        return left + right + 30;
    }
}

/// <summary>Part 31 of the fixture.</summary>
public sealed class Part031
{
    private readonly string _name = "Part 031: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 031)";
        }
        Count++;
        return text;
    }
    public int Add031(int left, int right)
    {
        return left + right + 31;
    }
}

/// <summary>Part 32 of the fixture.</summary>
public sealed class Part032
{
    private readonly string _name = "Part 032: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 032)";
        }
        Count++;
        return text;
    }
    public int Add032(int left, int right)
    {
        return left + right + 32;
    }
}

/// <summary>Part 33 of the fixture.</summary>
public sealed class Part033
{
    private readonly string _name = "Part 033: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 033)";
        }
        Count++;
        return text;
    }
    public int Add033(int left, int right)
    {
        return left + right + 33;
    }
}

/// <summary>Part 34 of the fixture.</summary>
public sealed class Part034
{
    private readonly string _name = "Part 034: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 034)";
        }
        Count++;
        return text;
    }
    public int Add034(int left, int right)
    {
        return left + right + 34;
    }
}

/// <summary>Part 35 of the fixture.</summary>
public sealed class Part035
{
    private readonly string _name = "Part 035: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 035)";
        }
        Count++;
        return text;
    }
    public int Add035(int left, int right)
    {
        return left + right + 35;
    }
}

/// <summary>Part 36 of the fixture.</summary>
public sealed class Part036
{
    private readonly string _name = "Part 036: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 036)";
        }
        Count++;
        return text;
    }
    public int Add036(int left, int right)
    {
        return left + right + 36;
    }
}

/// <summary>Part 37 of the fixture.</summary>
public sealed class Part037
{
    private readonly string _name = "Part 037: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 037)";
        }
        Count++;
        return text;
    }
    public int Add037(int left, int right)
    {
        return left + right + 37;
    }
}

/// <summary>Part 38 of the fixture.</summary>
public sealed class Part038
{
    private readonly string _name = "Part 038: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 038)";
        }
        Count++;
        return text;
    }
    public int Add038(int left, int right)
    {
        return left + right + 38;
    }
}

/// <summary>Part 39 of the fixture.</summary>
public sealed class Part039
{
    private readonly string _name = "Part 039: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 039)";
        }
        Count++;
        return text;
    }
    public int Add039(int left, int right)
    {
        return left + right + 39;
    }
}

/// <summary>Part 40 of the fixture.</summary>
public sealed class Part040
{
    private readonly string _name = "Part 040: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 040)";
        }
        Count++;
        return text;
    }
    public int Add040(int left, int right)
    {
        return left + right + 40;
    }
}

/// <summary>Part 41 of the fixture.</summary>
public sealed class Part041
{
    private readonly string _name = "Part 041: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 041)";
        }
        Count++;
        return text;
    }
    public int Add041(int left, int right)
    {
        return left + right + 41;
    }
}

/// <summary>Part 42 of the fixture.</summary>
public sealed class Part042
{
    private readonly string _name = "Part 042: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 042)";
        }
        Count++;
        return text;
    }
    public int Add042(int left, int right)
    {
        return left + right + 42;
    }
}

/// <summary>Part 43 of the fixture.</summary>
public sealed class Part043
{
    private readonly string _name = "Part 043: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 043)";
        }
        Count++;
        return text;
    }
    public int Add043(int left, int right)
    {
        return left + right + 43;
    }
}

/// <summary>Part 44 of the fixture.</summary>
public sealed class Part044
{
    private readonly string _name = "Part 044: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 044)";
        }
        Count++;
        return text;
    }
    public int Add044(int left, int right)
    {
        return left + right + 44;
    }
}

/// <summary>Part 45 of the fixture.</summary>
public sealed class Part045
{
    private readonly string _name = "Part 045: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 045)";
        }
        Count++;
        return text;
    }
    public int Add045(int left, int right)
    {
        return left + right + 45;
    }
}

/// <summary>Part 46 of the fixture.</summary>
public sealed class Part046
{
    private readonly string _name = "Part 046: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 046)";
        }
        Count++;
        return text;
    }
    public int Add046(int left, int right)
    {
        return left + right + 46;
    }
}

/// <summary>Part 47 of the fixture.</summary>
public sealed class Part047
{
    private readonly string _name = "Part 047: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 047)";
        }
        Count++;
        return text;
    }
    public int Add047(int left, int right)
    {
        return left + right + 47;
    }
}

/// <summary>Part 48 of the fixture.</summary>
public sealed class Part048
{
    private readonly string _name = "Part 048: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 048)";
        }
        Count++;
        return text;
    }
    public int Add048(int left, int right)
    {
        return left + right + 48;
    }
}

/// <summary>Part 49 of the fixture.</summary>
public sealed class Part049
{
    private readonly string _name = "Part 049: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 049)";
        }
        Count++;
        return text;
    }
    public int Add049(int left, int right)
    {
        return left + right + 49;
    }
}

/// <summary>Part 50 of the fixture.</summary>
public sealed class Part050
{
    private readonly string _name = "Part 050: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 050)";
        }
        Count++;
        return text;
    }
    public int Add050(int left, int right)
    {
        return left + right + 50;
    }
}

/// <summary>Part 51 of the fixture.</summary>
public sealed class Part051
{
    private readonly string _name = "Part 051: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 051)";
        }
        Count++;
        return text;
    }
    public int Add051(int left, int right)
    {
        return left + right + 51;
    }
}

/// <summary>Part 52 of the fixture.</summary>
public sealed class Part052
{
    private readonly string _name = "Part 052: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 052)";
        }
        Count++;
        return text;
    }
    public int Add052(int left, int right)
    {
        return left + right + 52;
    }
}

/// <summary>Part 53 of the fixture.</summary>
public sealed class Part053
{
    private readonly string _name = "Part 053: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 053)";
        }
        Count++;
        return text;
    }
    public int Add053(int left, int right)
    {
        return left + right + 53;
    }
}

/// <summary>Part 54 of the fixture.</summary>
public sealed class Part054
{
    private readonly string _name = "Part 054: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 054)";
        }
        Count++;
        return text;
    }
    public int Add054(int left, int right)
    {
        return left + right + 54;
    }
}

/// <summary>Part 55 of the fixture.</summary>
public sealed class Part055
{
    private readonly string _name = "Part 055: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 055)";
        }
        Count++;
        return text;
    }
    public int Add055(int left, int right)
    {
        return left + right + 55;
    }
}

/// <summary>Part 56 of the fixture.</summary>
public sealed class Part056
{
    private readonly string _name = "Part 056: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 056)";
        }
        Count++;
        return text;
    }
    public int Add056(int left, int right)
    {
        return left + right + 56;
    }
}

/// <summary>Part 57 of the fixture.</summary>
public sealed class Part057
{
    private readonly string _name = "Part 057: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 057)";
        }
        Count++;
        return text;
    }
    public int Add057(int left, int right)
    {
        return left + right + 57;
    }
}

/// <summary>Part 58 of the fixture.</summary>
public sealed class Part058
{
    private readonly string _name = "Part 058: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 058)";
        }
        Count++;
        return text;
    }
    public int Add058(int left, int right)
    {
        return left + right + 58;
    }
}

/// <summary>Part 59 of the fixture.</summary>
public sealed class Part059
{
    private readonly string _name = "Part 059: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 059)";
        }
        Count++;
        return text;
    }
    public int Add059(int left, int right)
    {
        return left + right + 59;
    }
}

/// <summary>Part 60 of the fixture.</summary>
public sealed class Part060
{
    private readonly string _name = "Part 060: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 060)";
        }
        Count++;
        return text;
    }
    public int Add060(int left, int right)
    {
        return left + right + 60;
    }
}

/// <summary>Part 61 of the fixture.</summary>
public sealed class Part061
{
    private readonly string _name = "Part 061: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 061)";
        }
        Count++;
        return text;
    }
    public int Add061(int left, int right)
    {
        return left + right + 61;
    }
}

/// <summary>Part 62 of the fixture.</summary>
public sealed class Part062
{
    private readonly string _name = "Part 062: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 062)";
        }
        Count++;
        return text;
    }
    public int Add062(int left, int right)
    {
        return left + right + 62;
    }
}

/// <summary>Part 63 of the fixture.</summary>
public sealed class Part063
{
    private readonly string _name = "Part 063: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 063)";
        }
        Count++;
        return text;
    }
    public int Add063(int left, int right)
    {
        return left + right + 63;
    }
}

/// <summary>Part 64 of the fixture.</summary>
public sealed class Part064
{
    private readonly string _name = "Part 064: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 064)";
        }
        Count++;
        return text;
    }
    public int Add064(int left, int right)
    {
        return left + right + 64;
    }
}

/// <summary>Part 65 of the fixture.</summary>
public sealed class Part065
{
    private readonly string _name = "Part 065: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 065)";
        }
        Count++;
        return text;
    }
    public int Add065(int left, int right)
    {
        return left + right + 65;
    }
}

/// <summary>Part 66 of the fixture.</summary>
public sealed class Part066
{
    private readonly string _name = "Part 066: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 066)";
        }
        Count++;
        return text;
    }
    public int Add066(int left, int right)
    {
        return left + right + 66;
    }
}

/// <summary>Part 67 of the fixture.</summary>
public sealed class Part067
{
    private readonly string _name = "Part 067: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 067)";
        }
        Count++;
        return text;
    }
    public int Add067(int left, int right)
    {
        return left + right + 67;
    }
}

/// <summary>Part 68 of the fixture.</summary>
public sealed class Part068
{
    private readonly string _name = "Part 068: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 068)";
        }
        Count++;
        return text;
    }
    public int Add068(int left, int right)
    {
        return left + right + 68;
    }
}

/// <summary>Part 69 of the fixture.</summary>
public sealed class Part069
{
    private readonly string _name = "Part 069: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 069)";
        }
        Count++;
        return text;
    }
    public int Add069(int left, int right)
    {
        return left + right + 69;
    }
}

/// <summary>Part 70 of the fixture.</summary>
public sealed class Part070
{
    private readonly string _name = "Part 070: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 070)";
        }
        Count++;
        return text;
    }
    public int Add070(int left, int right)
    {
        return left + right + 70;
    }
}

/// <summary>Part 71 of the fixture.</summary>
public sealed class Part071
{
    private readonly string _name = "Part 071: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 071)";
        }
        Count++;
        return text;
    }
    public int Add071(int left, int right)
    {
        return left + right + 71;
    }
}

/// <summary>Part 72 of the fixture.</summary>
public sealed class Part072
{
    private readonly string _name = "Part 072: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 072)";
        }
        Count++;
        return text;
    }
    public int Add072(int left, int right)
    {
        return left + right + 72;
    }
}

/// <summary>Part 73 of the fixture.</summary>
public sealed class Part073
{
    private readonly string _name = "Part 073: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 073)";
        }
        Count++;
        return text;
    }
    public int Add073(int left, int right)
    {
        return left + right + 73;
    }
}

/// <summary>Part 74 of the fixture.</summary>
public sealed class Part074
{
    private readonly string _name = "Part 074: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 074)";
        }
        Count++;
        return text;
    }
    public int Add074(int left, int right)
    {
        return left + right + 74;
    }
}

/// <summary>Part 75 of the fixture.</summary>
public sealed class Part075
{
    private readonly string _name = "Part 075: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 075)";
        }
        Count++;
        return text;
    }
    public int Add075(int left, int right)
    {
        return left + right + 75;
    }
}

/// <summary>Part 76 of the fixture.</summary>
public sealed class Part076
{
    private readonly string _name = "Part 076: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 076)";
        }
        Count++;
        return text;
    }
    public int Add076(int left, int right)
    {
        return left + right + 76;
    }
}

/// <summary>Part 77 of the fixture.</summary>
public sealed class Part077
{
    private readonly string _name = "Part 077: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 077)";
        }
        Count++;
        return text;
    }
    public int Add077(int left, int right)
    {
        return left + right + 77;
    }
}

/// <summary>Part 78 of the fixture.</summary>
public sealed class Part078
{
    private readonly string _name = "Part 078: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 078)";
        }
        Count++;
        return text;
    }
    public int Add078(int left, int right)
    {
        return left + right + 78;
    }
}

/// <summary>Part 79 of the fixture.</summary>
public sealed class Part079
{
    private readonly string _name = "Part 079: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 079)";
        }
        Count++;
        return text;
    }
    public int Add079(int left, int right)
    {
        return left + right + 79;
    }
}

/// <summary>Part 80 of the fixture.</summary>
public sealed class Part080
{
    private readonly string _name = "Part 080: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 080)";
        }
        Count++;
        return text;
    }
    public int Add080(int left, int right)
    {
        return left + right + 80;
    }
}

/// <summary>Part 81 of the fixture.</summary>
public sealed class Part081
{
    private readonly string _name = "Part 081: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 081)";
        }
        Count++;
        return text;
    }
    public int Add081(int left, int right)
    {
        return left + right + 81;
    }
}

/// <summary>Part 82 of the fixture.</summary>
public sealed class Part082
{
    private readonly string _name = "Part 082: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 082)";
        }
        Count++;
        return text;
    }
    public int Add082(int left, int right)
    {
        return left + right + 82;
    }
}

/// <summary>Part 83 of the fixture.</summary>
public sealed class Part083
{
    private readonly string _name = "Part 083: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 083)";
        }
        Count++;
        return text;
    }
    public int Add083(int left, int right)
    {
        return left + right + 83;
    }
}

/// <summary>Part 84 of the fixture.</summary>
public sealed class Part084
{
    private readonly string _name = "Part 084: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 084)";
        }
        Count++;
        return text;
    }
    public int Add084(int left, int right)
    {
        return left + right + 84;
    }
}

/// <summary>Part 85 of the fixture.</summary>
public sealed class Part085
{
    private readonly string _name = "Part 085: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 085)";
        }
        Count++;
        return text;
    }
    public int Add085(int left, int right)
    {
        return left + right + 85;
    }
}

/// <summary>Part 86 of the fixture.</summary>
public sealed class Part086
{
    private readonly string _name = "Part 086: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 086)";
        }
        Count++;
        return text;
    }
    public int Add086(int left, int right)
    {
        return left + right + 86;
    }
}

/// <summary>Part 87 of the fixture.</summary>
public sealed class Part087
{
    private readonly string _name = "Part 087: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 087)";
        }
        Count++;
        return text;
    }
    public int Add087(int left, int right)
    {
        return left + right + 87;
    }
}

/// <summary>Part 88 of the fixture.</summary>
public sealed class Part088
{
    private readonly string _name = "Part 088: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 088)";
        }
        Count++;
        return text;
    }
    public int Add088(int left, int right)
    {
        return left + right + 88;
    }
}

/// <summary>Part 89 of the fixture.</summary>
public sealed class Part089
{
    private readonly string _name = "Part 089: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 089)";
        }
        Count++;
        return text;
    }
    public int Add089(int left, int right)
    {
        return left + right + 89;
    }
}

/// <summary>Part 90 of the fixture.</summary>
public sealed class Part090
{
    private readonly string _name = "Part 090: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 090)";
        }
        Count++;
        return text;
    }
    public int Add090(int left, int right)
    {
        return left + right + 90;
    }
}

/// <summary>Part 91 of the fixture.</summary>
public sealed class Part091
{
    private readonly string _name = "Part 091: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 091)";
        }
        Count++;
        return text;
    }
    public int Add091(int left, int right)
    {
        return left + right + 91;
    }
}

/// <summary>Part 92 of the fixture.</summary>
public sealed class Part092
{
    private readonly string _name = "Part 092: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 092)";
        }
        Count++;
        return text;
    }
    public int Add092(int left, int right)
    {
        return left + right + 92;
    }
}

/// <summary>Part 93 of the fixture.</summary>
public sealed class Part093
{
    private readonly string _name = "Part 093: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 093)";
        }
        Count++;
        return text;
    }
    public int Add093(int left, int right)
    {
        return left + right + 93;
    }
}

/// <summary>Part 94 of the fixture.</summary>
public sealed class Part094
{
    private readonly string _name = "Part 094: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 094)";
        }
        Count++;
        return text;
    }
    public int Add094(int left, int right)
    {
        return left + right + 94;
    }
}

/// <summary>Part 95 of the fixture.</summary>
public sealed class Part095
{
    private readonly string _name = "Part 095: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 095)";
        }
        Count++;
        return text;
    }
    public int Add095(int left, int right)
    {
        return left + right + 95;
    }
}

/// <summary>Part 96 of the fixture.</summary>
public sealed class Part096
{
    private readonly string _name = "Part 096: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 096)";
        }
        Count++;
        return text;
    }
    public int Add096(int left, int right)
    {
        return left + right + 96;
    }
}

/// <summary>Part 97 of the fixture.</summary>
public sealed class Part097
{
    private readonly string _name = "Part 097: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 097)";
        }
        Count++;
        return text;
    }
    public int Add097(int left, int right)
    {
        return left + right + 97;
    }
}

/// <summary>Part 98 of the fixture.</summary>
public sealed class Part098
{
    private readonly string _name = "Part 098: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 098)";
        }
        Count++;
        return text;
    }
    public int Add098(int left, int right)
    {
        return left + right + 98;
    }
}

/// <summary>Part 99 of the fixture.</summary>
public sealed class Part099
{
    private readonly string _name = "Part 099: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 099)";
        }
        Count++;
        return text;
    }
    public int Add099(int left, int right)
    {
        return left + right + 99;
    }
}

/// <summary>Part 100 of the fixture.</summary>
public sealed class Part100
{
    private readonly string _name = "Part 100: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 100)";
        }
        Count++;
        return text;
    }
    public int Add100(int left, int right)
    {
        return left + right + 100;
    }
}

/// <summary>Part 101 of the fixture.</summary>
public sealed class Part101
{
    private readonly string _name = "Part 101: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 101)";
        }
        Count++;
        return text;
    }
    public int Add101(int left, int right)
    {
        return left + right + 101;
    }
}

/// <summary>Part 102 of the fixture.</summary>
public sealed class Part102
{
    private readonly string _name = "Part 102: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 102)";
        }
        Count++;
        return text;
    }
    public int Add102(int left, int right)
    {
        return left + right + 102;
    }
}

/// <summary>Part 103 of the fixture.</summary>
public sealed class Part103
{
    private readonly string _name = "Part 103: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 103)";
        }
        Count++;
        return text;
    }
    public int Add103(int left, int right)
    {
        return left + right + 103;
    }
}

/// <summary>Part 104 of the fixture.</summary>
public sealed class Part104
{
    private readonly string _name = "Part 104: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 104)";
        }
        Count++;
        return text;
    }
    public int Add104(int left, int right)
    {
        return left + right + 104;
    }
}

/// <summary>Part 105 of the fixture.</summary>
public sealed class Part105
{
    private readonly string _name = "Part 105: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 105)";
        }
        Count++;
        return text;
    }
    public int Add105(int left, int right)
    {
        return left + right + 105;
    }
}

/// <summary>Part 106 of the fixture.</summary>
public sealed class Part106
{
    private readonly string _name = "Part 106: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 106)";
        }
        Count++;
        return text;
    }
    public int Add106(int left, int right)
    {
        return left + right + 106;
    }
}

/// <summary>Part 107 of the fixture.</summary>
public sealed class Part107
{
    private readonly string _name = "Part 107: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 107)";
        }
        Count++;
        return text;
    }
    public int Add107(int left, int right)
    {
        return left + right + 107;
    }
}

/// <summary>Part 108 of the fixture.</summary>
public sealed class Part108
{
    private readonly string _name = "Part 108: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 108)";
        }
        Count++;
        return text;
    }
    public int Add108(int left, int right)
    {
        return left + right + 108;
    }
}

/// <summary>Part 109 of the fixture.</summary>
public sealed class Part109
{
    private readonly string _name = "Part 109: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 109)";
        }
        Count++;
        return text;
    }
    public int Add109(int left, int right)
    {
        return left + right + 109;
    }
}

/// <summary>Part 110 of the fixture.</summary>
public sealed class Part110
{
    private readonly string _name = "Part 110: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 110)";
        }
        Count++;
        return text;
    }
    public int Add110(int left, int right)
    {
        return left + right + 110;
    }
}

/// <summary>Part 111 of the fixture.</summary>
public sealed class Part111
{
    private readonly string _name = "Part 111: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 111)";
        }
        Count++;
        return text;
    }
    public int Add111(int left, int right)
    {
        return left + right + 111;
    }
}

/// <summary>Part 112 of the fixture.</summary>
public sealed class Part112
{
    private readonly string _name = "Part 112: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 112)";
        }
        Count++;
        return text;
    }
    public int Add112(int left, int right)
    {
        return left + right + 112;
    }
}

/// <summary>Part 113 of the fixture.</summary>
public sealed class Part113
{
    private readonly string _name = "Part 113: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 113)";
        }
        Count++;
        return text;
    }
    public int Add113(int left, int right)
    {
        return left + right + 113;
    }
}

/// <summary>Part 114 of the fixture.</summary>
public sealed class Part114
{
    private readonly string _name = "Part 114: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 114)";
        }
        Count++;
        return text;
    }
    public int Add114(int left, int right)
    {
        return left + right + 114;
    }
}

/// <summary>Part 115 of the fixture.</summary>
public sealed class Part115
{
    private readonly string _name = "Part 115: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 115)";
        }
        Count++;
        return text;
    }
    public int Add115(int left, int right)
    {
        return left + right + 115;
    }
}

/// <summary>Part 116 of the fixture.</summary>
public sealed class Part116
{
    private readonly string _name = "Part 116: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 116)";
        }
        Count++;
        return text;
    }
    public int Add116(int left, int right)
    {
        return left + right + 116;
    }
}

/// <summary>Part 117 of the fixture.</summary>
public sealed class Part117
{
    private readonly string _name = "Part 117: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 117)";
        }
        Count++;
        return text;
    }
    public int Add117(int left, int right)
    {
        return left + right + 117;
    }
}

/// <summary>Part 118 of the fixture.</summary>
public sealed class Part118
{
    private readonly string _name = "Part 118: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 118)";
        }
        Count++;
        return text;
    }
    public int Add118(int left, int right)
    {
        return left + right + 118;
    }
}

/// <summary>Part 119 of the fixture.</summary>
public sealed class Part119
{
    private readonly string _name = "Part 119: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 119)";
        }
        Count++;
        return text;
    }
    public int Add119(int left, int right)
    {
        return left + right + 119;
    }
}

/// <summary>Part 120 of the fixture.</summary>
public sealed class Part120
{
    private readonly string _name = "Part 120: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 120)";
        }
        Count++;
        return text;
    }
    public int Add120(int left, int right)
    {
        return left + right + 120;
    }
}

/// <summary>Part 121 of the fixture.</summary>
public sealed class Part121
{
    private readonly string _name = "Part 121: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 121)";
        }
        Count++;
        return text;
    }
    public int Add121(int left, int right)
    {
        return left + right + 121;
    }
}

/// <summary>Part 122 of the fixture.</summary>
public sealed class Part122
{
    private readonly string _name = "Part 122: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 122)";
        }
        Count++;
        return text;
    }
    public int Add122(int left, int right)
    {
        return left + right + 122;
    }
}

/// <summary>Part 123 of the fixture.</summary>
public sealed class Part123
{
    private readonly string _name = "Part 123: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 123)";
        }
        Count++;
        return text;
    }
    public int Add123(int left, int right)
    {
        return left + right + 123;
    }
}

/// <summary>Part 124 of the fixture.</summary>
public sealed class Part124
{
    private readonly string _name = "Part 124: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 124)";
        }
        Count++;
        return text;
    }
    public int Add124(int left, int right)
    {
        return left + right + 124;
    }
}

/// <summary>Part 125 of the fixture.</summary>
public sealed class Part125
{
    private readonly string _name = "Part 125: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 125)";
        }
        Count++;
        return text;
    }
    public int Add125(int left, int right)
    {
        return left + right + 125;
    }
}

/// <summary>Part 126 of the fixture.</summary>
public sealed class Part126
{
    private readonly string _name = "Part 126: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 126)";
        }
        Count++;
        return text;
    }
    public int Add126(int left, int right)
    {
        return left + right + 126;
    }
}

/// <summary>Part 127 of the fixture.</summary>
public sealed class Part127
{
    private readonly string _name = "Part 127: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 127)";
        }
        Count++;
        return text;
    }
    public int Add127(int left, int right)
    {
        return left + right + 127;
    }
}

/// <summary>Part 128 of the fixture.</summary>
public sealed class Part128
{
    private readonly string _name = "Part 128: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 128)";
        }
        Count++;
        return text;
    }
    public int Add128(int left, int right)
    {
        return left + right + 128;
    }
}

/// <summary>Part 129 of the fixture.</summary>
public sealed class Part129
{
    private readonly string _name = "Part 129: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 129)";
        }
        Count++;
        return text;
    }
    public int Add129(int left, int right)
    {
        return left + right + 129;
    }
}

/// <summary>Part 130 of the fixture.</summary>
public sealed class Part130
{
    private readonly string _name = "Part 130: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 130)";
        }
        Count++;
        return text;
    }
    public int Add130(int left, int right)
    {
        return left + right + 130;
    }
}

/// <summary>Part 131 of the fixture.</summary>
public sealed class Part131
{
    private readonly string _name = "Part 131: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 131)";
        }
        Count++;
        return text;
    }
    public int Add131(int left, int right)
    {
        return left + right + 131;
    }
}

/// <summary>Part 132 of the fixture.</summary>
public sealed class Part132
{
    private readonly string _name = "Part 132: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 132)";
        }
        Count++;
        return text;
    }
    public int Add132(int left, int right)
    {
        return left + right + 132;
    }
}

/// <summary>Part 133 of the fixture.</summary>
public sealed class Part133
{
    private readonly string _name = "Part 133: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 133)";
        }
        Count++;
        return text;
    }
    public int Add133(int left, int right)
    {
        return left + right + 133;
    }
}

/// <summary>Part 134 of the fixture.</summary>
public sealed class Part134
{
    private readonly string _name = "Part 134: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 134)";
        }
        Count++;
        return text;
    }
    public int Add134(int left, int right)
    {
        return left + right + 134;
    }
}

/// <summary>Part 135 of the fixture.</summary>
public sealed class Part135
{
    private readonly string _name = "Part 135: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 135)";
        }
        Count++;
        return text;
    }
    public int Add135(int left, int right)
    {
        return left + right + 135;
    }
}

/// <summary>Part 136 of the fixture.</summary>
public sealed class Part136
{
    private readonly string _name = "Part 136: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 136)";
        }
        Count++;
        return text;
    }
    public int Add136(int left, int right)
    {
        return left + right + 136;
    }
}

/// <summary>Part 137 of the fixture.</summary>
public sealed class Part137
{
    private readonly string _name = "Part 137: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 137)";
        }
        Count++;
        return text;
    }
    public int Add137(int left, int right)
    {
        return left + right + 137;
    }
}

/// <summary>Part 138 of the fixture.</summary>
public sealed class Part138
{
    private readonly string _name = "Part 138: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 138)";
        }
        Count++;
        return text;
    }
    public int Add138(int left, int right)
    {
        return left + right + 138;
    }
}

/// <summary>Part 139 of the fixture.</summary>
public sealed class Part139
{
    private readonly string _name = "Part 139: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 139)";
        }
        Count++;
        return text;
    }
    public int Add139(int left, int right)
    {
        return left + right + 139;
    }
}

/// <summary>Part 140 of the fixture.</summary>
public sealed class Part140
{
    private readonly string _name = "Part 140: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 140)";
        }
        Count++;
        return text;
    }
    public int Add140(int left, int right)
    {
        return left + right + 140;
    }
}

/// <summary>Part 141 of the fixture.</summary>
public sealed class Part141
{
    private readonly string _name = "Part 141: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 141)";
        }
        Count++;
        return text;
    }
    public int Add141(int left, int right)
    {
        return left + right + 141;
    }
}

/// <summary>Part 142 of the fixture.</summary>
public sealed class Part142
{
    private readonly string _name = "Part 142: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 142)";
        }
        Count++;
        return text;
    }
    public int Add142(int left, int right)
    {
        return left + right + 142;
    }
}

/// <summary>Part 143 of the fixture.</summary>
public sealed class Part143
{
    private readonly string _name = "Part 143: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 143)";
        }
        Count++;
        return text;
    }
    public int Add143(int left, int right)
    {
        return left + right + 143;
    }
}

/// <summary>Part 144 of the fixture.</summary>
public sealed class Part144
{
    private readonly string _name = "Part 144: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 144)";
        }
        Count++;
        return text;
    }
    public int Add144(int left, int right)
    {
        return left + right + 144;
    }
}

/// <summary>Part 145 of the fixture.</summary>
public sealed class Part145
{
    private readonly string _name = "Part 145: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 145)";
        }
        Count++;
        return text;
    }
    public int Add145(int left, int right)
    {
        return left + right + 145;
    }
}

/// <summary>Part 146 of the fixture.</summary>
public sealed class Part146
{
    private readonly string _name = "Part 146: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 146)";
        }
        Count++;
        return text;
    }
    public int Add146(int left, int right)
    {
        return left + right + 146;
    }
}

/// <summary>Part 147 of the fixture.</summary>
public sealed class Part147
{
    private readonly string _name = "Part 147: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 147)";
        }
        Count++;
        return text;
    }
    public int Add147(int left, int right)
    {
        return left + right + 147;
    }
}

/// <summary>Part 148 of the fixture.</summary>
public sealed class Part148
{
    private readonly string _name = "Part 148: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 148)";
        }
        Count++;
        return text;
    }
    public int Add148(int left, int right)
    {
        return left + right + 148;
    }
}

/// <summary>Part 149 of the fixture.</summary>
public sealed class Part149
{
    private readonly string _name = "Part 149: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 149)";
        }
        Count++;
        return text;
    }
    public int Add149(int left, int right)
    {
        return left + right + 149;
    }
}

/// <summary>Part 150 of the fixture.</summary>
public sealed class Part150
{
    private readonly string _name = "Part 150: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 150)";
        }
        Count++;
        return text;
    }
    public int Add150(int left, int right)
    {
        return left + right + 150;
    }
}

/// <summary>Part 151 of the fixture.</summary>
public sealed class Part151
{
    private readonly string _name = "Part 151: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 151)";
        }
        Count++;
        return text;
    }
    public int Add151(int left, int right)
    {
        return left + right + 151;
    }
}

/// <summary>Part 152 of the fixture.</summary>
public sealed class Part152
{
    private readonly string _name = "Part 152: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 152)";
        }
        Count++;
        return text;
    }
    public int Add152(int left, int right)
    {
        return left + right + 152;
    }
}

/// <summary>Part 153 of the fixture.</summary>
public sealed class Part153
{
    private readonly string _name = "Part 153: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 153)";
        }
        Count++;
        return text;
    }
    public int Add153(int left, int right)
    {
        return left + right + 153;
    }
}

/// <summary>Part 154 of the fixture.</summary>
public sealed class Part154
{
    private readonly string _name = "Part 154: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 154)";
        }
        Count++;
        return text;
    }
    public int Add154(int left, int right)
    {
        return left + right + 154;
    }
}

/// <summary>Part 155 of the fixture.</summary>
public sealed class Part155
{
    private readonly string _name = "Part 155: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 155)";
        }
        Count++;
        return text;
    }
    public int Add155(int left, int right)
    {
        return left + right + 155;
    }
}

/// <summary>Part 156 of the fixture.</summary>
public sealed class Part156
{
    private readonly string _name = "Part 156: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 156)";
        }
        Count++;
        return text;
    }
    public int Add156(int left, int right)
    {
        return left + right + 156;
    }
}

/// <summary>Part 157 of the fixture.</summary>
public sealed class Part157
{
    private readonly string _name = "Part 157: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 157)";
        }
        Count++;
        return text;
    }
    public int Add157(int left, int right)
    {
        return left + right + 157;
    }
}

/// <summary>Part 158 of the fixture.</summary>
public sealed class Part158
{
    private readonly string _name = "Part 158: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 158)";
        }
        Count++;
        return text;
    }
    public int Add158(int left, int right)
    {
        return left + right + 158;
    }
}

/// <summary>Part 159 of the fixture.</summary>
public sealed class Part159
{
    private readonly string _name = "Part 159: a string long enough to fill the user string heap of the fixture's file";
    public int Count { get; private set; }
    public string Describe(int times)
    {
        var text = _name;
        for (var n = 0; n < times; n++)
        {
            text += " (described again, number " + n + ", in part 159)";
        }
        Count++;
        return text;
    }
    public int Add159(int left, int right)
    {
        return left + right + 159;
    }
}
