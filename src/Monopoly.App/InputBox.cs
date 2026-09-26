using System.Windows;
using System.Windows.Controls;

namespace Monopoly.App
{
    // Простое окно для ввода числа (ставки)
    public class InputBox : Window
    {
        public string InputText { get; private set; } = "";
        private TextBox inputBox;
        private bool result = false;

        public InputBox(string prompt, string title)
        {
            Title = title;
            Width = 400;
            Height = 180;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.ToolWindow;

            var panel = new StackPanel { Margin = new Thickness(10) };
            panel.Children.Add(new TextBlock { Text = prompt, FontSize = 18, Margin = new Thickness(0, 0, 0, 10) });
            inputBox = new TextBox { FontSize = 18, Margin = new Thickness(0, 0, 0, 10) };
            panel.Children.Add(inputBox);

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var okBtn = new Button { Content = "OK", Width = 80, Margin = new Thickness(0, 0, 10, 0) };
            var cancelBtn = new Button { Content = "Отмена", Width = 80 };
            okBtn.Click += (s, e) => { InputText = inputBox.Text; result = true; Close(); };
            cancelBtn.Click += (s, e) => { result = false; Close(); };
            btnPanel.Children.Add(okBtn);
            btnPanel.Children.Add(cancelBtn);
            panel.Children.Add(btnPanel);

            Content = panel;
        }

        public new bool? ShowDialog()
        {
            base.ShowDialog();
            return result;
        }
    }
}
