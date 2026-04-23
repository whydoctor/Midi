namespace Chordara.Domain;

public enum Quality
{
    Major,
    Minor,
    Diminished,
    Augmented,
    Dominant7,
    Major7,
    Minor7,
    HalfDiminished7,
    Diminished7
}

public sealed record Chord(int RootMidi, Quality Quality, string Label)
{
    public int[] Voicing(int octave = 4)
    {
        int root = RootMidi + 12 * (octave - 4);
        return Quality switch
        {
            Quality.Major            => new[] { root, root + 4, root + 7 },
            Quality.Minor            => new[] { root, root + 3, root + 7 },
            Quality.Diminished       => new[] { root, root + 3, root + 6 },
            Quality.Augmented        => new[] { root, root + 4, root + 8 },
            Quality.Major7           => new[] { root, root + 4, root + 7, root + 11 },
            Quality.Minor7           => new[] { root, root + 3, root + 7, root + 10 },
            Quality.Dominant7        => new[] { root, root + 4, root + 7, root + 10 },
            Quality.HalfDiminished7  => new[] { root, root + 3, root + 6, root + 10 },
            Quality.Diminished7      => new[] { root, root + 3, root + 6, root + 9 },
            _                        => new[] { root }
        };
    }
}
