namespace Chordara.Domain;

public enum ScaleType { Major, NaturalMinor }

public static class Scale
{
    private static readonly int[] Major = { 0, 2, 4, 5, 7, 9, 11 };
    private static readonly int[] NaturalMinor = { 0, 2, 3, 5, 7, 8, 10 };

    public static int[] Degrees(ScaleType t) =>
        t == ScaleType.Major ? Major : NaturalMinor;

    public static ScaleType Parallel(ScaleType t) =>
        t == ScaleType.Major ? ScaleType.NaturalMinor : ScaleType.Major;

    public static readonly string[] PitchClassNames =
        { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    public static int PitchClassFromName(string name) => name.ToUpperInvariant() switch
    {
        "C" => 0, "C#" or "DB" => 1,
        "D" => 2, "D#" or "EB" => 3,
        "E" => 4, "F" => 5,
        "F#" or "GB" => 6, "G" => 7,
        "G#" or "AB" => 8, "A" => 9,
        "A#" or "BB" => 10, "B" => 11,
        _ => 0
    };
}
