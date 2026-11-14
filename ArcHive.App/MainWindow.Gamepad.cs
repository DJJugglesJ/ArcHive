using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using ArcHive.Core;

namespace ArcHive.App;

public partial class MainWindow
{
    private void StartGamepad()
    {
        var reducer = new GamepadNavigation(); var clock = Stopwatch.StartNew(); int? controller = null;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
        timer.Tick += (_, _) =>
        {
            var pad = XInputPad.Read();
            if (controller != pad?.Index) { reducer.Update(0, 0, 0, false, clock.Elapsed); controller = pad?.Index; }
            var editing = Keyboard.FocusedElement is TextBoxBase or PasswordBox or ComboBox or ComboBoxItem;
            var enabled = preferences.GamepadEnabled && IsActive && IsEnabled && operation is null && !editing && pad is not null;
            var action = reducer.Update(pad?.Buttons ?? 0, pad?.X ?? 0, pad?.Y ?? 0, enabled, clock.Elapsed);
            if (action == PadAction.None) return;
            if (action == PadAction.Accept) { Launch(this, new RoutedEventArgs()); return; }
            if (action == PadAction.Cancel) { Gallery.Focus(); return; }
            if (action is PadAction.Next or PadAction.Previous)
            {
                var current = archivesView ? 2 : FavoritesOnly.IsChecked == true ? 1 : 0;
                var next = (current + (action == PadAction.Next ? 1 : 2)) % 3;
                Navigate(next == 2, next == 1); return;
            }
            var index = GamepadNavigation.Move(Games.SelectedIndex, Games.Items.Count, archivesView ? 1 : galleryColumns, action);
            if (index < 0) return;
            Games.SelectedIndex = index;
            if (archivesView) ArchiveList.ScrollIntoView(Games.SelectedItem);
            else if (index / galleryColumns < Gallery.Items.Count) Gallery.ScrollIntoView(Gallery.Items[index / galleryColumns]);
        };
        Loaded += (_, _) => timer.Start(); Closed += (_, _) => timer.Stop();
    }
}
