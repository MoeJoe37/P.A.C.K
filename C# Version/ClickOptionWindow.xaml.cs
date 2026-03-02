using System.Windows;
namespace PACK
{
    public partial class ClickOptionWindow : Window
    {
        public int SelectedOption { get; private set; } = 3;
        public ClickOptionWindow() => InitializeComponent();
        private void OK_Click(object s, RoutedEventArgs e)
        {
            SelectedOption = Opt1.IsChecked == true ? 1 : Opt2.IsChecked == true ? 2 : 3;
            DialogResult = true;
        }
        private void Cancel_Click(object s, RoutedEventArgs e) => DialogResult = false;
    }
}
