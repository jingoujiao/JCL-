using System.Windows;

namespace MinecraftLuanch
{
    public partial class InputDialog : Window
    {
        public string InputText { get; private set; } = string.Empty;
        public bool IsConfirmed { get; private set; } = false;

        public InputDialog()
        {
            InitializeComponent();
            Loaded += InputDialog_Loaded;
        }

        public InputDialog(string prompt, string title, string defaultText = "") : this()
        {
            PromptText.Text = prompt;
            TitleText.Text = title;
            InputTextBox.Text = defaultText;
        }

        private void InputDialog_Loaded(object sender, RoutedEventArgs e)
        {
            InputTextBox.Focus();
            InputTextBox.SelectAll();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            InputText = InputTextBox.Text;
            IsConfirmed = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            Close();
        }

        public static string? Show(string prompt, string title, string defaultText = "")
        {
            var dialog = new InputDialog(prompt, title, defaultText);
            dialog.Owner = System.Windows.Application.Current.MainWindow;
            dialog.ShowDialog();
            return dialog.IsConfirmed ? dialog.InputText : null;
        }
    }
}
