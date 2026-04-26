using Chordara.Domain;
using NAudio.Wave;

namespace Chordara.Services;

/// <summary>
/// Polyphonic sine-wave synth with a small ASR envelope. No external
/// dependencies, no SoundFont — playback works out of the box.
/// </summary>
internal sealed class SineSampleProvider : ISampleProvider
{
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);

    private sealed class Voice
    {
        public double Frequency;
        public double Phase;
        public int    AgeSamples;
        public bool   Releasing;
        public int    ReleaseStartAge;
    }

    private readonly List<Voice> _voices = new();
    private readonly object _gate = new();

    private const float Gain           = 0.22f;
    private const float HarmonicMix    = 0.08f;
    private const int   AttackSamples  = 44100 / 80;     // ~12 ms
    private const int   ReleaseSamples = 44100 / 6;      // ~167 ms

    public void NoteOn(int midi)
    {
        lock (_gate)
        {
            _voices.Add(new Voice
            {
                Frequency  = MidiToFreq(midi),
                Phase      = Random.Shared.NextDouble() * 2.0 * Math.PI,
                AgeSamples = 0
            });
        }
    }

    /// <summary>Trigger release on every active voice.</summary>
    public void AllOff()
    {
        lock (_gate)
        {
            foreach (var v in _voices)
            {
                if (!v.Releasing)
                {
                    v.Releasing       = true;
                    v.ReleaseStartAge = v.AgeSamples;
                }
            }
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        lock (_gate)
        {
            double sr = WaveFormat.SampleRate;
            for (int i = 0; i < count; i++)
            {
                float sample = 0f;
                int active = 0;

                foreach (var v in _voices)
                {
                    float env = Envelope(v);
                    if (env > 0f)
                    {
                        float fund = (float)Math.Sin(v.Phase);
                        float harm = (float)Math.Sin(v.Phase * 2.0) * HarmonicMix;
                        sample += (fund + harm) * env;
                        active++;
                    }
                    v.Phase += 2.0 * Math.PI * v.Frequency / sr;
                    if (v.Phase > 2.0 * Math.PI) v.Phase -= 2.0 * Math.PI;
                    v.AgeSamples++;
                }

                // Soft normalization so a 4-note chord doesn't clip.
                if (active > 0)
                    sample = sample * Gain / (float)Math.Sqrt(active);

                buffer[offset + i] = sample;
            }

            // Reap finished voices.
            _voices.RemoveAll(v =>
                v.Releasing && (v.AgeSamples - v.ReleaseStartAge) >= ReleaseSamples + 64);
        }
        return count;
    }

    private static float Envelope(Voice v)
    {
        if (!v.Releasing)
        {
            if (v.AgeSamples < AttackSamples)
                return v.AgeSamples / (float)AttackSamples;
            return 1f;
        }
        int relAge = v.AgeSamples - v.ReleaseStartAge;
        if (relAge >= ReleaseSamples) return 0f;
        return 1f - relAge / (float)ReleaseSamples;
    }

    private static double MidiToFreq(int m) => 440.0 * Math.Pow(2.0, (m - 69) / 12.0);
}

public sealed class AudioEngine : IDisposable
{
    public bool IsReady => true;

    private readonly WaveOutEvent _out;
    private readonly SineSampleProvider _provider;
    private CancellationTokenSource? _cts;

    public AudioEngine()
    {
        _provider = new SineSampleProvider();
        _out = new WaveOutEvent { DesiredLatency = 80 };
        _out.Init(_provider);
        _out.Play();
    }

    public async Task PlayAsync(Progression prog, int bpm, int beatsPerChord = 4, CancellationToken ct = default)
    {
        Stop();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = _cts.Token;

        double secondsPerChord = 60.0 / bpm * beatsPerChord;

        try
        {
            foreach (var chord in prog.Chords)
            {
                token.ThrowIfCancellationRequested();
                _provider.AllOff();
                foreach (int p in chord.Voicing())
                    _provider.NoteOn(p);

                await Task.Delay(TimeSpan.FromSeconds(secondsPerChord), token);
            }
        }
        catch (OperationCanceledException) { /* normal stop */ }
        finally
        {
            _provider.AllOff();
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _provider.AllOff();
    }

    public void Dispose()
    {
        Stop();
        _out.Dispose();
    }
}
