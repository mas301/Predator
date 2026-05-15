using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using System;

namespace Predator;

public class MessageWindow : Window
{
    public MessageWindow(string message)
    {
        Title = "Mensaje";
        Width = 400;
        Height = 150;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;

        var stackPanel = new StackPanel
        {
            Spacing = 20,
            Margin = new Avalonia.Thickness(20),
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var messageTextBlock = new TextBlock
        {
            Text = message,
            FontSize = 14,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        stackPanel.Children.Add(messageTextBlock);

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 10
        };

        var okButton = new Button
        {
            Content = "OK",
            Padding = new Avalonia.Thickness(30, 10),
            Width = 100
        };
        okButton.Click += OnOkClick;
        buttonPanel.Children.Add(okButton);

        stackPanel.Children.Add(buttonPanel);

        Content = stackPanel;
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// Muestra un diálogo de mensaje de forma modal.
    /// </summary>
    /// <param name="message">El mensaje a mostrar.</param>
    /// <param name="parentWindow">La ventana padre (opcional).</param>
    public static async void ShowMessage(string message, Window? parentWindow = null)
    {
        var messageWindow = new MessageWindow(message);
        if (parentWindow != null)
        {
            await messageWindow.ShowDialog(parentWindow);
        }
        else
        {
            messageWindow.Show();
        }
    }
}
