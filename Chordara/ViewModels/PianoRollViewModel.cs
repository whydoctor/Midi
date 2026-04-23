using System.Collections.ObjectModel;
using Chordara.Domain;
using Chordara.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chordara.ViewModels;

public partial class PianoRollViewModel : ObservableObject
{
    public const double PixelsPerBeat = 40;
    public const double PixelsPerSemitone = 12;
    public const int LowestPitch = 36;   // C2
    public const int HighestPitch = 96;  // C7

    public ObservableCollection<NoteViewModel> Notes { get; } = new();

    public double CanvasHeight => (HighestPitch - LowestPitch + 1) * PixelsPerSemitone;

    [ObservableProperty] private double canvasWidth = 32 * PixelsPerBeat;

    public void Load(Progression prog, int beatsPerChord = 4)
    {
        Notes.Clear();
        for (int i = 0; i < prog.Chords.Count; i++)
        {
            foreach (int p in prog.Chords[i].Voicing())
            {
                Notes.Add(new NoteViewModel
                {
                    Pitch       = p,
                    StartBeat   = i * beatsPerChord,
                    LengthBeats = beatsPerChord
                });
            }
        }
        CanvasWidth = Math.Max(32 * PixelsPerBeat,
                               (prog.Chords.Count * beatsPerChord + 4) * PixelsPerBeat);
    }

    public NoteViewModel AddNote(double beat, int pitch, double length = 1)
    {
        var n = new NoteViewModel { Pitch = pitch, StartBeat = beat, LengthBeats = length };
        Notes.Add(n);
        return n;
    }

    public void Remove(NoteViewModel n) => Notes.Remove(n);

    public IEnumerable<PianoRollNote> ToExportNotes() =>
        Notes.Select(n => new PianoRollNote(n.Pitch, n.StartBeat, n.LengthBeats, n.Velocity));
}
