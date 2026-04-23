# Chordara

A small Windows desktop app (WPF / .NET 8 / C#) that generates chord
progressions, plays them with a SoundFont synth, lets you tweak them in a
piano roll, and exports / drags out a MIDI file.

## Features

- **Chord progression generator** — diatonic chords by mood, with **modal
  interchange** (borrowed chords from the parallel mode) and **secondary
  dominants** (`V/V`, `V7/vi`, `vii°7/V`, …).
- **Piano-roll editor** — click empty space to add a note, left-drag to
  move, right-edge handle to resize, right-click to delete.
- **Playback** — pure-C# SoundFont synth ([MeltySynth](https://github.com/sinshu/meltysynth))
  routed through NAudio. No external installs.
- **MIDI export** — `Export MIDI…` button writes a `.mid` file.
- **Drag-and-drop MIDI** — drag the **⇲ Drag MIDI** chip from the toolbar
  straight onto the desktop, Explorer, or your DAW.

## Build

Requires .NET 8 SDK on Windows.

```powershell
dotnet build -c Release
dotnet run   --project Chordara
```

## Self-contained single-file EXE

```powershell
dotnet publish Chordara -c Release -o publish
# -> publish\Chordara.exe (no external runtime required)
```

## SoundFont (required for playback)

Chordara looks for any `*.sf2` in `Chordara/Assets/` (next to the EXE
after publish) and loads the first one it finds. Without an SF2 you can
still generate, edit, export, and drag-out MIDI — only audio preview is
disabled.

Drop in any GM-compatible SoundFont. Small, freely-redistributable
options include:

- **TimGM6mb.sf2** (~6 MB, GM)
- **GeneralUser GS** (~30 MB, higher quality)

## Project layout

```
Chordara/
├─ Domain/        Note / Scale / Chord / Mood / Progression  (pure music theory)
├─ Services/      ChordGenerator · MidiExporter · AudioEngine
├─ ViewModels/    MainViewModel · PianoRollViewModel · NoteViewModel
├─ Views/         MainWindow + PianoRollView (+ Themes/Dark.xaml)
├─ Converters/    Beats↔px, Pitch↔px
└─ Assets/        Drop your .sf2 here
```

## Roman-numeral grammar (for `ChordGenerator`)

| Token     | Meaning                                       |
|-----------|-----------------------------------------------|
| `I`–`VII` | Major triad on that scale degree              |
| `i`–`vii` | Minor triad on that scale degree              |
| `°`       | Diminished                                    |
| `+`       | Augmented                                     |
| `7`       | Dominant 7 (uppercase) / minor 7 (lowercase)  |
| `maj7`    | Major 7                                       |
| `°7`      | Fully diminished 7                            |
| `ø` `ø7`  | Half-diminished 7                             |
| `b`       | Lower the root a semitone (`bVII`, `bII`, …)  |
| `X/Y`     | `X` built relative to `Y`'s root (sec. dom.)  |

Examples used by the mood templates:
`I-V/V-V-I`, `i-bII-V-i`, `IV-iv-I-V`, `vii°7/V-V-i`.

## Future improvements

- Voice-leading optimizer (smaller voicing jumps between chords)
- Inversions and bass-line generator
- MIDI input to seed the next chord
- Save / load project files
