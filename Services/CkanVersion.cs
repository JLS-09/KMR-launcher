using System;

namespace KMRLauncherMvvm.Services;

public static class CkanVersion
{
    public static int Compare(string a, string b)
    {
        var (epochA, restA) = SplitEpoch(a);
        var (epochB, restB) = SplitEpoch(b);

        if (epochA != epochB) return epochA.CompareTo(epochB);

        return CompareModVersion(restA, restB);
    }

    private static (long epoch, string rest) SplitEpoch(string version)
    {
        var colon = version.IndexOf(':');
        if (colon > 0 && long.TryParse(version[..colon], out var epoch) && epoch >= 0)
            return (epoch, version[(colon + 1)..]);

        return (0, version);
    }

    private static int CompareModVersion(string a, string b)
    {
        var i = 0;
        var j = 0;

        while (i < a.Length || j < b.Length)
        {
            var textA = TakeWhile(a, ref i, c => !char.IsAsciiDigit(c));
            var textB = TakeWhile(b, ref j, c => !char.IsAsciiDigit(c));

            var textCmp = CompareText(textA, textB);
            if (textCmp != 0) return textCmp;

            var numberA = TakeWhile(a, ref i, char.IsAsciiDigit);
            var numberB = TakeWhile(b, ref j, char.IsAsciiDigit);

            var numberCmp = CompareNumbers(numberA, numberB);
            if (numberCmp != 0) return numberCmp;
        }

        return 0;
    }

    private static string TakeWhile(string s, ref int index, Func<char, bool> predicate)
    {
        var start = index;
        while (index < s.Length && predicate(s[index])) index++;
        return s[start..index];
    }
    
    private static int CompareText(string a, string b)
    {
        var length = Math.Min(a.Length, b.Length);
        for (var k = 0; k < length; k++)
        {
            var cmp = CharOrder(a[k]).CompareTo(CharOrder(b[k]));
            if (cmp != 0) return cmp;
        }

        return a.Length.CompareTo(b.Length);
    }

    private static int CharOrder(char c) => char.IsAsciiLetter(c) ? c : c + 256;

    private static int CompareNumbers(string a, string b)
    {
        a = a.TrimStart('0');
        b = b.TrimStart('0');

        if (a.Length != b.Length) return a.Length.CompareTo(b.Length);
        return string.CompareOrdinal(a, b);
    }
}