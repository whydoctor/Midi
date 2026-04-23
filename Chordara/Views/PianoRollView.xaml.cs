using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Chordara.ViewModels;

namespace Chordara.Views;

public partial class PianoRollView : UserControl
{
    private const double SnapBeats = 0.25;
    private const double MinLength = 0.25;
    private const int    BarBeats  = 4;

    // Black-key pitch classes (C#, D#, F#, G#, A#)
    private static readonly HashSet<int> BlackKeys = new() { 1, 3, 6, 8, 10 };

    private enum DragMode { None, Move, Resize }

    private DragMode _mode = DragMode.None;
    private NoteViewModel? _drag;
    private Point _origin;
    private double _origStart;
    private int _origPitch;
    private double _origLength;
    private Canvas? _hostCanvas;
    private bool _suppressScrollSync;

    public PianoRollView() => InitializeComponent();

    /* ============================================================ */
    /*  Lifecycle                                                   */
    /* ============================================================ */

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        RedrawAll();
    }

    private void UserControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is INotifyPropertyChanged oldVm)
            oldVm.PropertyChanged -= Vm_PropertyChanged;
        if (e.NewValue is INotifyPropertyChanged newVm)
            newVm.PropertyChanged += Vm_PropertyChanged;

        RedrawAll();
    }

    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PianoRollViewModel.CanvasWidth))
            Dispatcher.BeginInvoke(new Action(RedrawGrid));
    }

    private void RedrawAll()
    {
        RedrawKeyboard();
        RedrawGrid();
    }

    /* ============================================================ */
    /*  Keyboard column                                             */
    /* ============================================================ */

    private void RedrawKeyboard()
    {
        if (DataContext is not PianoRollViewModel vm) return;

        KbCanvas.Children.Clear();
        double rowH  = PianoRollViewModel.PixelsPerSemitone;
        double width = KbCanvas.Width;

        var whiteFill  = (Brush)FindResource("Fg");
        var blackFill  = new SolidColorBrush(Color.FromRgb(0x16, 0x16, 0x1A));
        var dividerBr  = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x42));
        var labelBr    = new SolidColorBrush(Color.FromRgb(0x26, 0x26, 0x2C));

        for (int p = PianoRollViewModel.HighestPitch; p >= PianoRollViewModel.LowestPitch; p--)
        {
            int pc = ((p % 12) + 12) % 12;
            double y = (PianoRollViewModel.HighestPitch - p) * rowH;

            var row = new Rectangle
            {
                Width  = width,
                Height = rowH,
                Fill   = BlackKeys.Contains(pc) ? blackFill : whiteFill
            };
            Canvas.SetLeft(row, 0);
            Canvas.SetTop(row, y);
            KbCanvas.Children.Add(row);

            // Thin divider line under every row.
            var divider = new Rectangle
            {
                Width  = width,
                Height = 1,
                Fill   = dividerBr,
                Opacity = 0.4
            };
            Canvas.SetLeft(divider, 0);
            Canvas.SetTop(divider, y + rowH - 1);
            KbCanvas.Children.Add(divider);

            // Octave labels on every C.
            if (pc == 0)
            {
                int octave = (p / 12) - 1;
                var text = new TextBlock
                {
                    Text       = $"C{octave}",
                    FontSize   = 10,
                    Foreground = labelBr,
                    FontWeight = FontWeights.SemiBold
                };
                Canvas.SetLeft(text, 6);
                Canvas.SetTop(text, y - 1);
                KbCanvas.Children.Add(text);
            }
        }
    }

    /* ============================================================ */
    /*  Grid backdrop                                               */
    /* ============================================================ */

    private void RedrawGrid()
    {
        if (DataContext is not PianoRollViewModel vm) return;

        GridCanvas.Children.Clear();
        double rowH = PianoRollViewModel.PixelsPerSemitone;
        double beatW = PianoRollViewModel.PixelsPerBeat;
        double w = vm.CanvasWidth;
        double h = vm.CanvasHeight;

        GridCanvas.Width  = w;
        GridCanvas.Height = h;

        var blackRowFill = new SolidColorBrush(Color.FromArgb(0x26, 0x00, 0x00, 0x00));
        var beatLine     = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x30));
        var barLine      = new SolidColorBrush(Color.FromRgb(0x45, 0x45, 0x52));
        var octaveLine   = new SolidColorBrush(Color.FromArgb(0x60, 0x5B, 0xA8, 0xFF));

        // Row shading (black-key rows) + octave gridlines at C.
        for (int p = PianoRollViewModel.HighestPitch; p >= PianoRollViewModel.LowestPitch; p--)
        {
            int pc = ((p % 12) + 12) % 12;
            double y = (PianoRollViewModel.HighestPitch - p) * rowH;

            if (BlackKeys.Contains(pc))
            {
                var shade = new Rectangle { Width = w, Height = rowH, Fill = blackRowFill };
                Canvas.SetLeft(shade, 0);
                Canvas.SetTop(shade, y);
                GridCanvas.Children.Add(shade);
            }

            if (pc == 0)
            {
                var octLine = new Rectangle { Width = w, Height = 1, Fill = octaveLine, Opacity = 0.5 };
                Canvas.SetLeft(octLine, 0);
                Canvas.SetTop(octLine, y + rowH - 1);
                GridCanvas.Children.Add(octLine);
            }
        }

        // Vertical beat / bar lines.
        int totalBeats = (int)Math.Ceiling(w / beatW);
        for (int b = 0; b <= totalBeats; b++)
        {
            bool isBar = b % BarBeats == 0;
            var line = new Rectangle
            {
                Width  = isBar ? 1.5 : 1,
                Height = h,
                Fill   = isBar ? barLine : beatLine,
                Opacity = isBar ? 0.9 : 0.55
            };
            Canvas.SetLeft(line, b * beatW);
            Canvas.SetTop(line, 0);
            GridCanvas.Children.Add(line);
        }
    }

    /* ============================================================ */
    /*  Scroll sync                                                 */
    /* ============================================================ */

    private void RollScroller_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_suppressScrollSync) return;
        if (Math.Abs(e.VerticalChange) < 0.5) return;

        _suppressScrollSync = true;
        KbScroller.ScrollToVerticalOffset(e.VerticalOffset);
        _suppressScrollSync = false;
    }

    // Forward wheel events from the keyboard column to the main roll so the
    // two stay in sync (Hidden scrollbar still scrolls on its own otherwise).
    private void KbScroller_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        RollScroller.ScrollToVerticalOffset(RollScroller.VerticalOffset - e.Delta);
        e.Handled = true;
    }

    /* ============================================================ */
    /*  Notes: find the items panel                                 */
    /* ============================================================ */

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
        beat  = Math.Round(beat / SnapBeats) * SnapBeats;
        pitch = Math.Clamp(pitch, PianoRollViewModel.LowestPitch, PianoRollViewModel.HighestPitch);
        return (beat, pitch);
    }

    /* ============================================================ */
    /*  Mouse: add / move / resize / delete                         */
    /* ============================================================ */

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not PianoRollViewModel vm) return;
        if (e.OriginalSource is Rectangle or Grid) return; // hit a note, not empty space

        var canvas = FindNotesCanvas();
        if (canvas is null) return;

        var (beat, pitch) = PointToMusic(e.GetPosition(canvas));
        vm.AddNote(beat, pitch, 1);
        e.Handled = true;
    }

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
        canvas.MouseMove         += Canvas_MouseMove;
        canvas.MouseLeftButtonUp += Canvas_MouseLeftButtonUp;
        e.Handled = true;
    }

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
        canvas.MouseMove         += Canvas_MouseMove;
        canvas.MouseLeftButtonUp += Canvas_MouseLeftButtonUp;
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
