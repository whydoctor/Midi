using ChordForge.Domain;
using MeltySynth;
using NAudio.Wave;

namespace ChordForge.Services;

/// <summary>
/// Bridges MeltySynth (pure-C# SoundFont synth) into NAudio's WaveOutEvent.
/// Stereo, IEEE float, 44.1 kHz.
/// </summary>
internal sealed class MeltySynthSampleProvider : ISampleProvider
{
    private readonly Synthesizer _synth;
    private readonly float[] _left;
    private readonly float[] _right;
    private readonly object _gate = new();

    public WaveFormat WaveFormat { get; }

    public MeltySynthSampleProvider(Synthesizer synth, int blockSize = 1024)
    {
        _synth  = synth;
        _left   = new float[blockSize];
        _right  = new float[blockSize];
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(synth.SampleRate, 2);
    }

    public void NoteOn(int channel, int key, int velocity)
    {
        lock (_gate) _synth.NoteOn(channel, key, velocity);
    }

    public void NoteOff(int channel, int key)
    {
        lock (_gate) _synth.NoteOff(channel, key);
    }

    public void AllNotesOff()
    {
        lock (_gate) _synth.NoteOffAll(false);
    }

    public void ProgramChange(int channel, int program)
    {
        lock (_gate) _synth.ProcessMidiMessage(channel, 0xC0, program, 0);
    }

    public int Read(float[] buffer, int offset, int count)
    {
        // count is total interleaved samples (2 per frame).
        int frames     = count / 2;
        int framesDone = 0;

        while (framesDone < frames)
        {
            int chunk = Math.Min(_left.Length, frames - framesDone);
            lock (_gate)
                _synth.Render(_left.AsSpan(0, chunk), _right.AsSpan(0, chunk));

            for (int i = 0; i < chunk; i++)
            {
                int idx = offset + (framesDone + i) * 2;
                buffer[idx]     = _left[i];
                buffer[idx + 1] = _right[i];
            }
            framesDone += chunk;
        }
        return count;
    }
}

public sealed class AudioEngine : IDisposable
{
    public bool IsReady { get; private set; }
    public string? LoadedSoundFontPath { get; private set; }

    private WaveOutEvent? _out;
    private MeltySynthSampleProvider? _provider;
    private CancellationTokenSource? _cts;

    /// <summary>
    /// Loads any .sf2 in the Assets folder (next to the executable). Returns false if
    /// none was found; playback will be a no-op until a SoundFont is supplied.
    /// </summary>
    public bool TryAutoLoadSoundFont()
    {
        string baseDir = AppContext.BaseDirectory;
        string assets  = Path.Combine(baseDir, "Assets");
        if (!Directory.Exists(assets)) return false;

        string? sf2 = Directory.EnumerateFiles(assets, "*.sf2").FirstOrDefault();
        return sf2 != null && LoadSoundFont(sf2);
    }

    public bool LoadSoundFont(string path)
    {
        if (!File.Exists(path)) return false;

        Stop();
        _out?.Dispose();

        var synth = new Synthesizer(path, 44100);
        _provider = new MeltySynthSampleProvider(synth);
        _out = new WaveOutEvent { DesiredLatency = 80 };
        _out.Init(_provider);
        _out.Play();

        // GM program 0 = Acoustic Grand Piano. Many SF2s default elsewhere.
        _provider.ProgramChange(0, 0);

        LoadedSoundFontPath = path;
        IsReady = true;
        return true;
    }

    public async Task PlayAsync(Progression prog, int bpm, int beatsPerChord = 4, CancellationToken ct = default)
    {
        if (!IsReady || _provider is null) return;

        Stop();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = _cts.Token;

        double secondsPerChord = 60.0 / bpm * beatsPerChord;

        try
        {
            foreach (var chord in prog.Chords)
            {
                token.ThrowIfCancellationRequested();
                _provider.AllNotesOff();
                foreach (int p in chord.Voicing())
                    _provider.NoteOn(0, p, 96);

                await Task.Delay(TimeSpan.FromSeconds(secondsPerChord), token);
            }
        }
        catch (OperationCanceledException) { /* normal stop */ }
        finally
        {
            _provider.AllNotesOff();
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _provider?.AllNotesOff();
    }

    public void Dispose()
    {
        Stop();
        _out?.Dispose();
        _out = null;
    }
}
