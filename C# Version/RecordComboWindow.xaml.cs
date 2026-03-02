using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace PACK
{
    public partial class RecordComboWindow : Window
    {
        public string? RecordedCombo { get; private set; }

        private readonly HashSet<Key> _held = new();

        public RecordComboWindow() => InitializeComponent();

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            var k = e.Key == Key.System ? e.SystemKey : e.Key;
            _held.Add(k);
            ComboDisplay.Text = BuildLabel();
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            var k = e.Key == Key.System ? e.SystemKey : e.Key;
            _held.Remove(k);
        }

        private string BuildLabel()
        {
            var parts = new List<string>();
            if (_held.Contains(Key.LeftCtrl)  || _held.Contains(Key.RightCtrl))  parts.Add("Ctrl");
            if (_held.Contains(Key.LeftShift) || _held.Contains(Key.RightShift)) parts.Add("Shift");
            if (_held.Contains(Key.LeftAlt)   || _held.Contains(Key.RightAlt))   parts.Add("Alt");

            foreach (var k in _held)
            {
                if (k is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
                      or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin) continue;
                parts.Add(k.ToString());
            }
            return parts.Count == 0 ? "(press any key…)" : string.Join("+", parts);
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            _held.Clear();
            ComboDisplay.Text = "(press any key…)";
        }

        private void Done_Click(object sender, RoutedEventArgs e)
        {
            string label = ComboDisplay.Text;
            if (label == "(press any key…)" || string.IsNullOrWhiteSpace(label))
            { MessageBox.Show("No key combo recorded.", "Nothing Set", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            RecordedCombo = label;
            DialogResult  = true;
        }
    }
}
