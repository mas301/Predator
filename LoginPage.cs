using Avalonia.Controls;
using Avalonia.Layout;
using CoreIA;
using System.Data;
using System;

namespace Predator;

public class LoginPage : UserControl
{
    private const double InputWidth = 220;

    public Sesion? SesionActual { get; set; }

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
            Text = "20604217327",
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
            Text = "Desarrollador",
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
            Text = "ghs2020",
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

    private async void OnContinueButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isPostLoginState)
            return;

        if (SesionActual is null)
        {
            MessageWindow.ShowMessage("La sesión no está inicializada.");
            return;
        }

        var codigoEmpresa = _empresaTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(codigoEmpresa))
        {
            MessageWindow.ShowMessage("Ingrese el código de empresa.");
            return;
        }

        var cadenaConexion = SesionActual.ConexionDominio;
        if (string.IsNullOrWhiteSpace(cadenaConexion))
        {
            MessageWindow.ShowMessage("La conexión de sesión no está configurada.");
            return;
        }

        DataSet dts;
        try
        {
            dts = await Logeo.LeeTokenAsync(codigoEmpresa, cadenaConexion);

        }
        catch (Exception ex)
        {
            MessageWindow.ShowMessage("Ha ocurrido un error al intentar acceder al servidor: " + ex.Message);
            return;
        }
        System.Data.DataRow? dtr = null;
        if (dts.Tables.Count > 0 && dts.Tables[0].Rows.Count > 0)
        {
            dtr = dts.Tables[0].Rows[0];
        }
        else
        {
            MessageWindow.ShowMessage("La Empresa no existe.");
            return;
        }

        string ConexionInicial = "Data Source=***;Initial Catalog=###;User Id=sa###;Password=Mauricio2004;TrustServerCertificate=True;Encrypt=True;";
        SesionActual.ConexionCrud = ConexionInicial.Replace("***", Globales.Texto(dtr["Servidor"])).Replace("###", Globales.Texto(dtr["BaseDatos"]));

        var LOM = new CoreIA.Clases.LoginyMensaje();
        SesionActual.Credenciales = new Credenciales
        {
            CodigoEmpresa = _empresaTextBox.Text ?? string.Empty,
            Usuario = _usuarioTextBox.Text ?? string.Empty,
            Contrasenia = _contrasenaTextBox.Text ?? string.Empty,
        };
        var res = await Logeo.LogeoAsync(SesionActual, LOM);
        if (res)
        {
            var login = LOM.Login;
            if (login is null)
                return;

            SesionActual.Credenciales.EmpresaId = Globales.EnteroNull(login["EmpresaId"]);
            SesionActual.Credenciales.Empresa = Globales.Texto(login["Empresa"]);
            SesionActual.Credenciales.SedeId = Globales.EnteroNull(login["SedeId"]);
            SesionActual.Credenciales.UsuarioId = Globales.EnteroNull(login["UsuarioId"]);
            SesionActual.Credenciales.Id = Globales.Texto(login["Sesion"]);
            SesionActual.Credenciales.Master = Globales.Booleano(login["Master"]);

            _nombreEmpresaTextBox.Text = SesionActual.Credenciales.Empresa;


            _nombreEmpresaPanel.IsVisible = true;
            _contrasenaPanel.IsVisible = false;
            _sedePanel.IsVisible = true;
            _continueButton.IsVisible = false;
            _recordarCheckBox.IsVisible = false;
            _logoutButton.IsVisible = true;
            _isPostLoginState = true;
            LoginContinued?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            MessageWindow.ShowMessage("Usuario o contraseña incorrectos.");
            return;
        }

        
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
