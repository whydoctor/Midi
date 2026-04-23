using System.Windows;
using System.Windows.Input;
using ChordForge.ViewModels;

namespace ChordForge.Views;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        (DataContext as IDisposable)?.Dispose();
    }

    /// <summary>
    /// Drag-out: writes the current piano roll to a temp .mid and starts a
    /// FileDrop drag operation. The user can drop the file onto Explorer, the
    /// desktop, Ableton, FL Studio, Reaper, etc.
    /// </summary>
    private void DragChip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        try
        {
            string path = vm.PrepareDragMidi();
            var data = new DataObject(DataFormats.FileDrop, new[] { path });
            DragDrop.DoDragDrop(DragChip, data, DragDropEffects.Copy);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "Could not prepare MIDI for drag-out:\n\n" + ex.Message,
                "ChordForge", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
