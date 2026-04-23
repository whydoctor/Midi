using CommunityToolkit.Mvvm.ComponentModel;

namespace Chordara.ViewModels;

public partial class NoteViewModel : ObservableObject
{
    [ObservableProperty] private int pitch;          // MIDI 0-127
    [ObservableProperty] private double startBeat;
    [ObservableProperty] private double lengthBeats;
    [ObservableProperty] private int velocity = 96;
}
