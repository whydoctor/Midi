using Chordara.Domain;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace Chordara.Services;

public readonly record struct PianoRollNote(int Pitch, double StartBeat, double LengthBeats, int Velocity = 96);

public sealed class MidiExporter
{
    public const short TicksPerQuarter = 480;

    public void ExportProgression(Progression prog, int bpm, string path, int beatsPerChord = 4)
    {
        var notes = new List<PianoRollNote>();
        for (int i = 0; i < prog.Chords.Count; i++)
        {
            double start = i * beatsPerChord;
            foreach (int p in prog.Chords[i].Voicing())
                notes.Add(new PianoRollNote(p, start, beatsPerChord));
        }
        ExportNotes(notes, bpm, path);
    }

    public void ExportNotes(IEnumerable<PianoRollNote> notes, int bpm, string path)
    {
        var tempoMap = TempoMap.Create(
            new TicksPerQuarterNoteTimeDivision(TicksPerQuarter),
            Tempo.FromBeatsPerMinute(bpm));

        var midiNotes = notes.Select(n =>
        {
            long start  = (long)Math.Round(n.StartBeat  * TicksPerQuarter);
            long length = Math.Max(1, (long)Math.Round(n.LengthBeats * TicksPerQuarter));
            return new Note(
                noteNumber: (SevenBitNumber)Clamp7(n.Pitch),
                length:     length,
                time:       start)
            {
                Velocity = (SevenBitNumber)Clamp7(n.Velocity),
                Channel  = (FourBitNumber)0
            };
        }).ToList();

        var trackChunk = midiNotes.ToTrackChunk();
        var file = new MidiFile(trackChunk);
        file.ReplaceTempoMap(tempoMap);
        file.Write(path, overwriteFile: true);
    }

    private static int Clamp7(int v) => Math.Clamp(v, 0, 127);
}
