using System.Text;

namespace Keyboop.Core;

/// <summary>Physical-key mapping for common US QWERTY / Russian ЙЦУКЕН layouts.
/// Does not infer correctness: explicit user-triggered conversion only.</summary>
public static class LayoutConverter
{
    private const string LatinLower = "`qwertyuiop[]asdfghjkl;'zxcvbnm,./";
    private const string CyrillicLower = "ёйцукенгшщзхъфывапролджэячсмитьбю.";
    private static readonly Dictionary<char,char> LatToRu = Build(LatinLower, CyrillicLower);
    private static readonly Dictionary<char,char> RuToLat = Build(CyrillicLower, LatinLower);
    private static Dictionary<char,char> Build(string source, string target)
    {
        if (source.Length != target.Length) throw new InvalidOperationException("Keyboard tables mismatch");
        var m = new Dictionary<char,char>();
        for (var i=0; i<source.Length; i++) m[source[i]]=target[i];
        return m;
    }

    public static string Convert(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var lat=0; var ru=0;
        foreach(var ch in input)
        {
            if(ch is >= 'a' and <= 'z' or >= 'A' and <= 'Z') lat++;
            if(ch is >= 'а' and <= 'я' or >= 'А' and <= 'Я' or 'ё' or 'Ё') ru++;
        }
        if(lat==ru) return input; // mixed/ambiguous: do nothing
        var map=lat>ru?LatToRu:RuToLat;
        var result = new StringBuilder(input.Length);
        foreach(var ch in input)
        {
            var lower=char.ToLowerInvariant(ch);
            if(map.TryGetValue(lower,out var c))result.Append(char.IsUpper(ch)?char.ToUpperInvariant(c):c);
            else result.Append(ch);
        }
        return result.ToString();
    }
}