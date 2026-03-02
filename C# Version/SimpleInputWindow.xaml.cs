using System.Windows;
using System.Windows.Input;

namespace PACK
{
    public partial class SimpleInputWindow : Window
    {
        public string? Result { get; private set; }

        public SimpleInputWindow(string title, string prompt, string defaultValue = "")
        {
            InitializeComponent();
            Title             = title;
            PromptLabel.Text  = prompt;
            InputBox.Text     = defaultValue;
            InputBox.SelectAll();
            InputBox.Focus();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            Result       = InputBox.Text;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { Result = InputBox.Text; DialogResult = true; }
        }
    }
}
