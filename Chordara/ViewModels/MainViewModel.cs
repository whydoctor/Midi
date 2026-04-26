using Chordara.Domain;
using Chordara.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.IO;

namespace Chordara.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly ChordGenerator _generator = new();
    private readonly MidiExporter   _exporter  = new();
    private readonly AudioEngine    _audio     = new();

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

        IsPlaying = true;
        StatusMessage = "Playing…";
        try { await _audio.PlayAsync(_current, Bpm, BeatsPerChord); }
        finally
        {
            IsPlaying = false;
            StatusMessage = "Ready.";
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
