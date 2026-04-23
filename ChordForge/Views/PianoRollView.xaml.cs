using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ChordForge.ViewModels;

namespace ChordForge.Views;

public partial class PianoRollView : UserControl
{
    private const double SnapBeats = 0.25;
    private const double MinLength = 0.25;

    private enum DragMode { None, Move, Resize }

    private DragMode _mode = DragMode.None;
    private NoteViewModel? _drag;
    private Point _origin;
    private double _origStart;
    private int _origPitch;
    private double _origLength;
    private Canvas? _hostCanvas;

    public PianoRollView() => InitializeComponent();

    private Canvas? FindNotesCanvas() =>
        _hostCanvas ??= FindVisual<Canvas>(NotesItems, "NotesCanvas");

    private static T? FindVisual<T>(DependencyObject root, string name) where T : FrameworkElement
    {
        if (root is T t && t.Name == name) return t;
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var found = FindVisual<T>(VisualTreeHelper.GetChild(root, i), name);
            if (found != null) return found;
        }
        return null;
    }

    private (double beat, int pitch) PointToMusic(Point p)
    {
        double beat  = Math.Max(0, p.X / PianoRollViewModel.PixelsPerBeat);
        int    pitch = PianoRollViewModel.HighestPitch
                       - (int)Math.Round(p.Y / PianoRollViewModel.PixelsPerSemitone);
        beat = Math.Round(beat / SnapBeats) * SnapBeats;
        pitch = Math.Clamp(pitch, PianoRollViewModel.LowestPitch, PianoRollViewModel.HighestPitch);
        return (beat, pitch);
    }

    /* ---------- Add note on empty area ---------- */
    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not PianoRollViewModel vm) return;
        if (e.OriginalSource is Rectangle or Grid) return; // hit a note, not the canvas

        var canvas = FindNotesCanvas();
        if (canvas is null) return;

        var (beat, pitch) = PointToMusic(e.GetPosition(canvas));
        vm.AddNote(beat, pitch, 1);
        e.Handled = true;
    }

    /* ---------- Move existing note ---------- */
    private void Note_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        if (fe.DataContext is not NoteViewModel n) return;

        var canvas = FindNotesCanvas();
        if (canvas is null) return;

        _mode       = DragMode.Move;
        _drag       = n;
        _origin     = e.GetPosition(canvas);
        _origStart  = n.StartBeat;
        _origPitch  = n.Pitch;
        _origLength = n.LengthBeats;

        canvas.CaptureMouse();
        canvas.MouseMove          += Canvas_MouseMove;
        canvas.MouseLeftButtonUp  += Canvas_MouseLeftButtonUp;
        e.Handled = true;
    }

    /* ---------- Resize handle ---------- */
    private void Resize_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        if (fe.DataContext is not NoteViewModel n) return;

        var canvas = FindNotesCanvas();
        if (canvas is null) return;

        _mode       = DragMode.Resize;
        _drag       = n;
        _origin     = e.GetPosition(canvas);
        _origStart  = n.StartBeat;
        _origPitch  = n.Pitch;
        _origLength = n.LengthBeats;

        canvas.CaptureMouse();
        canvas.MouseMove          += Canvas_MouseMove;
        canvas.MouseLeftButtonUp  += Canvas_MouseLeftButtonUp;
        e.Handled = true;
    }

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_drag is null || sender is not Canvas canvas) return;
        var p = e.GetPosition(canvas);

        double dxBeats = (p.X - _origin.X) / PianoRollViewModel.PixelsPerBeat;
        int    dySemi  = (int)Math.Round((p.Y - _origin.Y) / PianoRollViewModel.PixelsPerSemitone);

        if (_mode == DragMode.Move)
        {
            double newStart = Math.Max(0, _origStart + dxBeats);
            newStart = Math.Round(newStart / SnapBeats) * SnapBeats;
            int newPitch = Math.Clamp(_origPitch - dySemi,
                                      PianoRollViewModel.LowestPitch,
                                      PianoRollViewModel.HighestPitch);
            _drag.StartBeat = newStart;
            _drag.Pitch     = newPitch;
        }
        else if (_mode == DragMode.Resize)
        {
            double newLen = Math.Max(MinLength, _origLength + dxBeats);
            newLen = Math.Round(newLen / SnapBeats) * SnapBeats;
            _drag.LengthBeats = newLen;
        }
    }

    private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Canvas canvas) return;
        canvas.MouseMove         -= Canvas_MouseMove;
        canvas.MouseLeftButtonUp -= Canvas_MouseLeftButtonUp;
        canvas.ReleaseMouseCapture();
        _drag = null;
        _mode = DragMode.None;
    }

    /* ---------- Right click deletes ---------- */
    private void Note_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not PianoRollViewModel vm) return;
        if (sender is FrameworkElement fe && fe.DataContext is NoteViewModel n)
        {
            vm.Remove(n);
            e.Handled = true;
        }
    }
}
