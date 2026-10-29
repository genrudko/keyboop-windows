namespace Keyboop.Core;

/// <summary>
/// Deterministic physical A–Z key mapping for standard English QWERTY
/// and Russian ЙЦУКЕН (not an IME or a full international layout resolver).
/// </summary>
public static class PhysicalKeyMapper
{
    private const string English = "abcdefghijklmnopqrstuvwxyz";
    private const string Russian = "фисвуапршолдьтщзйкыегмцчня";

    public static bool TryMapLetter(uint virtualKey, ushort primaryLanguage, bool shift, bool capsLock, out char letter)
    {
        letter = default;
        if(virtualKey < 0x41 || virtualKey > 0x5A) return false;
        if(primaryLanguage != 0x0009 && primaryLanguage != 0x0019) return false;
        int index=(int)(virtualKey-0x41);
        if(Russian.Length != English.Length) throw new InvalidOperationException("Physical keyboard mapping table mismatch.");
        char c=primaryLanguage==0x0019?Russian[index]:English[index];
        letter=shift ^ capsLock ? char.ToUpperInvariant(c) : c;
        return true;
    }
}
