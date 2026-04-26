using Chordara.Domain;
using Chordara.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace Chordara.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly ChordGenerator _generator = new();
    private readonly MidiExporter   _exporter  = new();
    private readonly AudioEngine    _audio     = new();
    private readonly AppSettings    _settings  = AppSettings.Load();

    public IReadOnlyList<string>     Keys     { get; } = Scale.PitchClassNames;
    public IReadOnlyList<ScaleType>  Scales   { get; } = Enum.GetValues<ScaleType>();
    public IReadOnlyList<Mood>       Moods    { get; } = Enum.GetValues<Mood>();
    public IReadOnlyList<int>        Lengths  { get; } = new[] { 4, 8, 12, 16 };

    [ObservableProperty] private string     selectedKey   = "C";
    [ObservableProperty] private ScaleType  selectedScale = ScaleType.Major;
    [ObservableProperty] private Mood       selectedMood  = Mood.Happy;
    [ObservableProperty] private int        length        = 8;
    [ObservableProperty] private int        bpm           = 100;
    [ObservableProperty] private int        beatsPerChord = 4;
    [ObservableProperty] private string     statusMessage = "Ready.";
    [ObservableProperty] private string     progressionLabel = "";
    [ObservableProperty] private bool       isPlaying;

    public PianoRollViewModel PianoRoll { get; } = new();

    private Progression? _current;

    public MainViewModel()
    {
        // Try (in order): .sf2 in /Assets, then last user-loaded path, then give up.
        if (_audio.TryAutoLoadSoundFont() ||
            (!string.IsNullOrWhiteSpace(_settings.SoundFontPath) &&
             _audio.LoadSoundFont(_settings.SoundFontPath!)))
        {
            StatusMessage = $"SoundFont: {Path.GetFileName(_audio.LoadedSoundFontPath)}";
        }
        else
        {
            StatusMessage = "No SoundFont loaded — click 'Load SoundFont…' to enable playback.";
        }
    }

    [RelayCommand]
    private void Generate()
    {
        int tonic = 60 + Scale.PitchClassFromName(SelectedKey); // C4 = 60
        _current = _generator.Generate(tonic, SelectedScale, SelectedMood, Length);
        PianoRoll.Load(_current, BeatsPerChord);
        ProgressionLabel = _current.Display;
    }

    [RelayCommand]
    private async Task Play()
    {
        if (_current is null) Generate();
        if (_current is null) return;

        if (!_audio.IsReady)
        {
            StatusMessage = "No SoundFont loaded — click 'Load SoundFont…' to enable playback.";
            return;
        }

        IsPlaying = true;
        try { await _audio.PlayAsync(_current, Bpm, BeatsPerChord); }
        finally { IsPlaying = false; }
    }

    [RelayCommand]
    private void LoadSoundFont()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "SoundFont files (*.sf2)|*.sf2|All files (*.*)|*.*",
            Title  = "Choose a SoundFont (.sf2)"
        };
        if (dlg.ShowDialog() != true) return;

        if (_audio.LoadSoundFont(dlg.FileName))
        {
            _settings.SoundFontPath = dlg.FileName;
            _settings.Save();
            StatusMessage = $"SoundFont: {Path.GetFileName(dlg.FileName)}";
        }
        else
        {
            StatusMessage = $"Could not load SoundFont: {Path.GetFileName(dlg.FileName)}";
        }
    }

    [RelayCommand]
    private void Stop()
    {
        _audio.Stop();
        IsPlaying = false;
    }

    [RelayCommand]
    private void Export()
    {
        if (PianoRoll.Notes.Count == 0) Generate();

        var dlg = new SaveFileDialog
        {
            Filter   = "MIDI files (*.mid)|*.mid",
            FileName = SuggestFileName()
        };
        if (dlg.ShowDialog() != true) return;

        _exporter.ExportNotes(PianoRoll.ToExportNotes(), Bpm, dlg.FileName);
        StatusMessage = $"Exported {Path.GetFileName(dlg.FileName)}";
    }

    /// <summary>
    /// Writes the current piano roll to a temp .mid and returns its path,
    /// for use as a drag-and-drop payload to Explorer / DAWs.
    /// </summary>
    public string PrepareDragMidi()
    {
        if (PianoRoll.Notes.Count == 0) Generate();

        string dir  = Path.Combine(Path.GetTempPath(), "Chordara");
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, SuggestFileName());

        _exporter.ExportNotes(PianoRoll.ToExportNotes(), Bpm, file);
        StatusMessage = $"Drag-out ready: {Path.GetFileName(file)}";
        return file;
    }

    private string SuggestFileName() =>
        $"Chordara_{SelectedKey}_{SelectedScale}_{SelectedMood}_{Bpm}bpm.mid"
            .Replace('#', 's');

    public void Dispose()
    {
        _audio.Dispose();
    }
}
