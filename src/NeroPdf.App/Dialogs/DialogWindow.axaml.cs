using Avalonia.Controls;
using Avalonia.Input;

namespace NeroPdf.App.Dialogs;

/// <summary>A simple modal dialog: heading, message, optional password field and buttons.</summary>
public partial class DialogWindow : Window
{
    public DialogWindow()
    {
        InitializeComponent();
    }

    /// <summary>The password typed when a button was pressed. The caller clears it right after use.</summary>
    public string? EnteredPassword { get; set; }

    /// <param name="buttons">Result value and text of each button; the first one is the default button.</param>
    public static DialogWindow Create(string title, string message, IReadOnlyList<(string Result, string Text)> buttons,
        string? cancelResult, bool withPassword = false, string? error = null)
    {
        var dialog = new DialogWindow { Title = title };
        dialog.Heading.Text = title;
        dialog.Message.Text = message;
        dialog.Password.IsVisible = withPassword;
        dialog.ErrorText.Text = error;
        dialog.ErrorText.IsVisible = !string.IsNullOrEmpty(error);

        for (var i = 0; i < buttons.Count; i++)
        {
            var (result, text) = buttons[i];
            var button = new Button
            {
                Content = text,
                IsDefault = i == 0,
                IsCancel = result == cancelResult,
                Classes = { i == 0 ? "dialog-primary" : "dialog-secondary" },
            };
            button.Click += (_, _) =>
            {
                dialog.EnteredPassword = dialog.Password.Text;
                dialog.Close(result);
            };
            dialog.Buttons.Children.Add(button);
        }
        dialog.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && cancelResult is not null)
            {
                dialog.Close(cancelResult);
            }
        };
        return dialog;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (Password.IsVisible)
        {
            Password.Focus();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        Password.Text = string.Empty;
    }
}
