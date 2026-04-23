using ChordForge.Domain;

namespace ChordForge.Services;

/// <summary>
/// Generates diatonic chord progressions with optional modal interchange
/// (borrowed chords from the parallel mode) and secondary dominants
/// (notation: V/V, V7/vi, vii°7/V, etc.).
///
/// Roman-numeral grammar:
///   IV, V, VII   uppercase  -> major triad
///   ii, vi, iii  lowercase  -> minor triad
///   ° = diminished, + = augmented
///   7 suffix    -> dominant 7 (uppercase) or minor 7 (lowercase)
///   maj7        -> major 7
///   °7          -> fully-diminished 7
///   ø  / ø7     -> half-diminished 7
///   b prefix    -> root lowered a semitone (bVII, bVI, bIII, bII Neapolitan)
///   X/Y         -> X built relative to Y (secondary dominant / leading-tone)
/// </summary>
public sealed class ChordGenerator
{
    private readonly Random _rng;

    public ChordGenerator(int? seed = null) =>
        _rng = seed.HasValue ? new Random(seed.Value) : new Random();

    // Per-mood progression templates. Mixed: pure diatonic, modal interchange, and
    // secondary dominants. All mood pools work in either major or minor; the
    // parser interprets the numerals against the chosen tonic + scale.
    private static readonly Dictionary<Mood, string[][]> Templates = new()
    {
        [Mood.Happy] = new[]
        {
            new[] { "I", "V", "vi", "IV" },
            new[] { "I", "IV", "V", "I" },
            new[] { "I", "vi", "IV", "V" },
            new[] { "I", "V/V", "V", "I" },          // secondary dominant lift
            new[] { "I", "IV", "V/vi", "vi" },
        },
        [Mood.Sad] = new[]
        {
            new[] { "vi", "IV", "I", "V" },
            new[] { "i", "VI", "III", "VII" },
            new[] { "i", "iv", "v", "i" },
            new[] { "I", "iv", "I", "V" },           // borrowed iv (modal interchange)
            new[] { "vi", "ii", "V/vi", "vi" },
        },
        [Mood.Dark] = new[]
        {
            new[] { "i", "bVI", "bIII", "bVII" },
            new[] { "i", "bII", "V", "i" },          // Neapolitan + dominant
            new[] { "i", "iv", "bII", "V" },
            new[] { "i", "bVI", "V/V", "V" },        // chromatic ascent into V
            new[] { "i", "V/iv", "iv", "V" },
        },
        [Mood.Uplifting] = new[]
        {
            new[] { "IV", "I", "V", "vi" },
            new[] { "I", "iii", "IV", "V" },
            new[] { "IV", "V/vi", "vi", "V" },       // secondary dominant climb
            new[] { "I", "V/IV", "IV", "V" },        // I7 functioning as V/IV
            new[] { "vi", "IV", "I", "V" },
        },
        [Mood.Nostalgic] = new[]
        {
            new[] { "I", "vi", "IV", "V" },          // 50s doo-wop
            new[] { "I", "vi", "ii", "V" },
            new[] { "I", "V/vi", "vi", "IV" },
            new[] { "IV", "iv", "I", "V" },          // bittersweet borrowed iv
            new[] { "Imaj7", "vi7", "ii7", "V7" },
        },
        [Mood.Tense] = new[]
        {
            new[] { "i", "bII", "V", "i" },
            new[] { "ii°", "V7", "i", "i" },
            new[] { "i", "V/V", "V", "i" },
            new[] { "vii°7/V", "V", "i", "i" },
            new[] { "i", "iv", "V7", "i" },
        },
    };

    public Progression Generate(int tonicMidi, ScaleType scale, Mood mood, int length)
    {
        var pool = Templates[mood];
        var chords = new List<Chord>(length);

        while (chords.Count < length)
        {
            var pattern = pool[_rng.Next(pool.Length)];
            foreach (var roman in pattern)
            {
                if (chords.Count == length) break;
                chords.Add(BuildChord(tonicMidi, scale, roman));
            }
        }
        return new Progression(chords);
    }

    /// <summary>Parse a single roman-numeral token (with optional /target) into a Chord.</summary>
    public static Chord BuildChord(int tonicMidi, ScaleType scale, string roman)
    {
        // Secondary chord: "V7/vi" -> build V7 in the key whose tonic is vi's root.
        if (roman.Contains('/'))
        {
            var parts = roman.Split('/', 2);
            var target = BuildChord(tonicMidi, scale, parts[1]);
            var subScale = ScaleForTarget(target.Quality);
            var sub = BuildChord(target.RootMidi, subScale, parts[0]);
            return sub with { Label = roman };
        }

        string body = roman;

        // Leading accidentals (we only use 'b' in our templates, but support '#' too).
        int accidental = 0;
        while (body.Length > 0 && (body[0] == 'b' || body[0] == '#'))
        {
            // Don't consume a leading lowercase letter that happens to be 'b' (none of i-vii is 'b').
            if (body[0] == 'b') accidental -= 1;
            else accidental += 1;
            body = body[1..];
        }

        // Trailing quality / extension markers (order matters).
        bool dom7 = false, maj7 = false, dim = false, aug = false, halfDim = false, dim7 = false;

        if (EndsWithCi(body, "maj7")) { maj7 = true;    body = body[..^4]; }
        else if (body.EndsWith("ø7")) { halfDim = true; body = body[..^2]; }
        else if (body.EndsWith("ø"))  { halfDim = true; body = body[..^1]; }
        else if (body.EndsWith("°7")) { dim7 = true;    body = body[..^2]; }
        else if (body.EndsWith("°"))  { dim = true;     body = body[..^1]; }
        else if (body.EndsWith("+"))  { aug = true;     body = body[..^1]; }
        else if (body.EndsWith("7"))  { dom7 = true;    body = body[..^1]; }

        if (body.Length == 0)
            throw new ArgumentException($"Could not parse roman numeral '{roman}'.");

        bool isMinor = char.IsLower(body[0]);
        int degreeIndex = body.ToUpperInvariant() switch
        {
            "I"   => 0,
            "II"  => 1,
            "III" => 2,
            "IV"  => 3,
            "V"   => 4,
            "VI"  => 5,
            "VII" => 6,
            _ => throw new ArgumentException($"Unknown numeral '{body}' in '{roman}'.")
        };

        int root = tonicMidi + Scale.Degrees(scale)[degreeIndex] + accidental;

        Quality q =
            dim7    ? Quality.Diminished7      :
            halfDim ? Quality.HalfDiminished7  :
            dim     ? Quality.Diminished       :
            aug     ? Quality.Augmented        :
            maj7    ? (isMinor ? Quality.Minor7    : Quality.Major7)    :
            dom7    ? (isMinor ? Quality.Minor7    : Quality.Dominant7) :
                      (isMinor ? Quality.Minor     : Quality.Major);

        return new Chord(root, q, roman);
    }

    private static ScaleType ScaleForTarget(Quality q) => q switch
    {
        Quality.Minor or Quality.Minor7
            or Quality.Diminished or Quality.HalfDiminished7 or Quality.Diminished7
            => ScaleType.NaturalMinor,
        _   => ScaleType.Major,
    };

    private static bool EndsWithCi(string s, string suffix) =>
        s.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
}
