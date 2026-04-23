namespace ChordForge.Domain;

public sealed record Progression(IReadOnlyList<Chord> Chords)
{
    public string Display => string.Join(" - ", Chords.Select(c => c.Label));
}
