using System;
using System.Runtime.CompilerServices;
using Keyboop.Core;

internal static class PhysicalKeySmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var tests = new (uint key, ushort lang, bool shift, bool caps, char expected)[]
        {
            (0x47, 0x0009, false, false, 'g'),
            (0x47, 0x0019, false, false, 'п'),
            (0x51, 0x0019, false, false, 'й'),
            (0x41, 0x0019, false, false, 'ф'),
            (0x5A, 0x0019, false, false, 'я'),
            (0x47, 0x0019, true, false, 'П'),
            (0x47, 0x0019, false, true, 'П'),
            (0x47, 0x0019, true, true, 'п')
        };
        foreach (var (key, lang, shift, caps, expected) in tests)
        {
            if (!PhysicalKeyMapper.TryMapLetter(key, lang, shift, caps, out var actual)
                || actual != expected)
                throw new Exception($"Physical map mismatch: {key}, {lang}, {expected}, {actual}");
        }
        if (PhysicalKeyMapper.TryMapLetter(0x47, 0x000c, false, false, out _))
            throw new Exception("Unexpected mapping for unsupported language");
        Console.WriteLine("PASS 9 physical key mapping cases");
    }
}
