using System.Globalization;
using System.Windows.Data;
using Chordara.ViewModels;

namespace Chordara.Converters;

public sealed class BeatsToPxConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => System.Convert.ToDouble(value) * PianoRollViewModel.PixelsPerBeat;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => System.Convert.ToDouble(value) / PianoRollViewModel.PixelsPerBeat;
}

public sealed class PitchToPxConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        int pitch = System.Convert.ToInt32(value);
        return (PianoRollViewModel.HighestPitch - pitch) * PianoRollViewModel.PixelsPerSemitone;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double y = System.Convert.ToDouble(value);
        return PianoRollViewModel.HighestPitch - (int)(y / PianoRollViewModel.PixelsPerSemitone);
    }
}
