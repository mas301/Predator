using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;

namespace Predator;

public class LoginPage : UserControl
{
    private const double InputWidth = 220;

    public event EventHandler? LoginContinued;
    public event EventHandler? LogoutRequested;

    private TextBox _empresaTextBox = null!;
    private TextBox _nombreEmpresaTextBox = null!;
    private TextBox _usuarioTextBox = null!;
    private TextBox _contrasenaTextBox = null!;
    private ComboBox _sedeComboBox = null!;
    private StackPanel _nombreEmpresaPanel = null!;
    private StackPanel _contrasenaPanel = null!;
    private StackPanel _sedePanel = null!;
    private Button _continueButton = null!;
    private Button _logoutButton = null!;
    private CheckBox _recordarCheckBox = null!;
    private bool _isPostLoginState;

    public LoginPage()
    {
        InitializeComponents();
    }

    private void InitializeComponents()
    {
        var mainStackPanel = new StackPanel
        {
            Spacing = 20,
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Avalonia.Thickness(0)
        };

        // Título Login
        var titleTextBlock = new TextBlock
        {
            Text = "Login",
            FontSize = 24,
            FontWeight = Avalonia.Media.FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        mainStackPanel.Children.Add(titleTextBlock);

        // Código de Empresa
        var empresaStackPanel = new StackPanel { Spacing = 4 };
        empresaStackPanel.Children.Add(new TextBlock
        {
            Text = "Código de Empresa",
            FontSize = 12
        });
        _empresaTextBox = new TextBox
        {
            PlaceholderText = "Ingrese el código de empresa",
            Width = InputWidth
        };
        empresaStackPanel.Children.Add(_empresaTextBox);
        mainStackPanel.Children.Add(empresaStackPanel);

        // Nombre de la Empresa
        _nombreEmpresaPanel = new StackPanel { Spacing = 4, IsVisible = false };
        _nombreEmpresaPanel.Children.Add(new TextBlock
        {
            Text = "Nombre de la Empresa",
            FontSize = 12
        });
        _nombreEmpresaTextBox = new TextBox
        {
            PlaceholderText = "Ingrese el nombre de la empresa",
            Width = InputWidth
        };
        _nombreEmpresaPanel.Children.Add(_nombreEmpresaTextBox);
        mainStackPanel.Children.Add(_nombreEmpresaPanel);

        // Cuenta de Usuario
        var usuarioStackPanel = new StackPanel { Spacing = 4 };
        usuarioStackPanel.Children.Add(new TextBlock
        {
            Text = "Cuenta de Usuario",
            FontSize = 12
        });
        _usuarioTextBox = new TextBox
        {
            PlaceholderText = "Ingrese la cuenta de usuario",
            Width = InputWidth
        };
        usuarioStackPanel.Children.Add(_usuarioTextBox);
        mainStackPanel.Children.Add(usuarioStackPanel);

        // Contraseña
        _contrasenaPanel = new StackPanel { Spacing = 4 };
        _contrasenaPanel.Children.Add(new TextBlock
        {
            Text = "Contraseña",
            FontSize = 12
        });
        _contrasenaTextBox = new TextBox
        {
            PasswordChar = '*',
            PlaceholderText = "Ingrese la contraseña",
            Width = InputWidth
        };
        _contrasenaPanel.Children.Add(_contrasenaTextBox);
        mainStackPanel.Children.Add(_contrasenaPanel);

        _sedePanel = new StackPanel
        {
            Spacing = 4,
            IsVisible = false
        };
        _sedePanel.Children.Add(new TextBlock
        {
            Text = "Sede",
            FontSize = 12
        });
        _sedeComboBox = new ComboBox
        {
            PlaceholderText = "Seleccione la sede",
            ItemsSource = new[] { "Principal", "Norte", "Sur", "Centro" },
            SelectedIndex = 0,
            Width = InputWidth
        };
        _sedePanel.Children.Add(_sedeComboBox);
        mainStackPanel.Children.Add(_sedePanel);

        // Botones Continuar y Logout
        var buttonContainerPanel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Orientation = Orientation.Horizontal,
            Spacing = 0
        };

        _continueButton = new Button
        {
            Content = "Continuar",
            Padding = new Avalonia.Thickness(30, 10),
            IsVisible = true
        };
        _continueButton.Click += OnContinueButtonClick;
        buttonContainerPanel.Children.Add(_continueButton);

        _logoutButton = new Button
        {
            Content = "Logout",
            Padding = new Avalonia.Thickness(30, 10),
            IsVisible = false
        };
        _logoutButton.Click += OnLogoutButtonClick;
        buttonContainerPanel.Children.Add(_logoutButton);

        mainStackPanel.Children.Add(buttonContainerPanel);

        // Recordar Credenciales
        _recordarCheckBox = new CheckBox
        {
            Content = "Recordar Credenciales",
            HorizontalAlignment = HorizontalAlignment.Center,
            IsVisible = true,
            FontSize = 12
        };
        mainStackPanel.Children.Add(_recordarCheckBox);

        // Border contenedor
        var border = new Border
        {
            Padding = new Avalonia.Thickness(40, 10, 40, 40),
            Child = mainStackPanel
        };

        Content = border;
    }

    private void OnContinueButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!_isPostLoginState)
        {
            _contrasenaPanel.IsVisible = false;
            _sedePanel.IsVisible = true;
            _continueButton.IsVisible = false;
            _recordarCheckBox.IsVisible = false;
            _logoutButton.IsVisible = true;
            _nombreEmpresaPanel.IsVisible = true;
            _isPostLoginState = true;
        }

        LoginContinued?.Invoke(this, EventArgs.Empty);
    }

    private void OnLogoutButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Regresar al estado inicial
        _contrasenaPanel.IsVisible = true;
        _sedePanel.IsVisible = false;
        _continueButton.IsVisible = true;
        _recordarCheckBox.IsVisible = true;
        _logoutButton.IsVisible = false;
        _nombreEmpresaPanel.IsVisible = false;
        _isPostLoginState = false;

        LogoutRequested?.Invoke(this, EventArgs.Empty);
    }
}
