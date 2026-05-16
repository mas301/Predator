using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace Predator;

public class DateRangeFilterControl : Border
{
    private readonly Button _filterButton;
    private readonly Popup _popup;
    private readonly Calendar _startCalendar;
    private readonly Calendar _endCalendar;
    private DateTime? _appliedStartDate;
    private DateTime? _appliedEndDate;

    public event EventHandler<DateRangeChangedEventArgs>? DateRangeChanged;

    public DateTime? StartDate => _appliedStartDate;
    public DateTime? EndDate => _appliedEndDate;

    public DateRangeFilterControl()
    {
        Background = Brushes.White;
        BorderThickness = new Avalonia.Thickness(0.5);
        BorderBrush = Brushes.LightGray;
        Padding = new Avalonia.Thickness(0);

        // Crear los Calendars primero
        _startCalendar = new Calendar
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            DisplayMode = CalendarMode.Month,
            SelectionMode = CalendarSelectionMode.SingleDate
        };
        
        _endCalendar = new Calendar
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            DisplayMode = CalendarMode.Month,
            SelectionMode = CalendarSelectionMode.SingleDate
        };

        _filterButton = new Button
        {
            Content = "📅",
            FontSize = 16,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = Brushes.White,
            BorderThickness = new Avalonia.Thickness(0),
            Padding = new Avalonia.Thickness(0)
        };

        var popupContent = CreatePopupContent();
        _popup = new Popup
        {
            Child = popupContent,
            PlacementTarget = _filterButton,
            IsLightDismissEnabled = false
        };

        _filterButton.Click += OnFilterButtonClick;

        // Crear un Grid para contener el botón y el popup
        var grid = new Grid();
        grid.Children.Add(_filterButton);
        grid.Children.Add(_popup);
        
        Child = grid;
    }

    private void OnFilterButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _popup.IsOpen = !_popup.IsOpen;
    }

    private Border CreatePopupContent()
    {
        var border = new Border
        {
            Background = Brushes.White,
            BorderBrush = Brushes.Gray,
            BorderThickness = new Avalonia.Thickness(1),
            Padding = new Avalonia.Thickness(10),
            BoxShadow = new BoxShadows(new BoxShadow { Blur = 10, Color = Colors.Black, OffsetY = 2 })
        };

        var mainPanel = new StackPanel
        {
            Spacing = 10,
            MinWidth = 500
        };

        // Título
        var titleBlock = new TextBlock
        {
            Text = "Filtrar por rango de fechas",
            FontWeight = FontWeight.Bold,
            FontSize = 13,
            Margin = new Avalonia.Thickness(0, 0, 0, 5)
        };
        mainPanel.Children.Add(titleBlock);

        // Calendarios en horizontal
        var calendarsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 15
        };

        // Fecha Inicial
        var startPanel = new StackPanel { Spacing = 4 };
        startPanel.Children.Add(new TextBlock
        {
            Text = "Fecha Inicial:",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold
        });
        startPanel.Children.Add(_startCalendar);
        calendarsPanel.Children.Add(startPanel);

        // Separador
        calendarsPanel.Children.Add(new Border
        {
            Width = 1,
            Background = Brushes.LightGray,
            Margin = new Avalonia.Thickness(0, 20, 0, 0)
        });

        // Fecha Final
        var endPanel = new StackPanel { Spacing = 4 };
        endPanel.Children.Add(new TextBlock
        {
            Text = "Fecha Final:",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold
        });
        endPanel.Children.Add(_endCalendar);
        calendarsPanel.Children.Add(endPanel);

        mainPanel.Children.Add(calendarsPanel);

        // Botones
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(0, 10, 0, 0)
        };

        var okButton = new Button
        {
            Content = "OK",
            Width = 80,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        okButton.Click += OnOkClick;

        var cancelButton = new Button
        {
            Content = "Cancelar",
            Width = 80,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        cancelButton.Click += OnCancelClick;

        var clearButton = new Button
        {
            Content = "Limpiar",
            Width = 80,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        clearButton.Click += OnClearClick;

        buttonPanel.Children.Add(clearButton);
        buttonPanel.Children.Add(cancelButton);
        buttonPanel.Children.Add(okButton);

        mainPanel.Children.Add(buttonPanel);

        border.Child = mainPanel;
        return border;
    }

    private void OnOkClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _appliedStartDate = _startCalendar.SelectedDate?.Date;
        _appliedEndDate = _endCalendar.SelectedDate?.Date.AddHours(23).AddMinutes(59).AddSeconds(59);

        UpdateButtonText();
        _popup.IsOpen = false;

        DateRangeChanged?.Invoke(this, new DateRangeChangedEventArgs(_appliedStartDate, _appliedEndDate));
    }

    private void OnCancelClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Restaurar valores aplicados
        _startCalendar.SelectedDate = _appliedStartDate;
        _endCalendar.SelectedDate = _appliedEndDate?.Date;

        _popup.IsOpen = false;
    }

    private void OnClearClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _startCalendar.SelectedDate = null;
        _endCalendar.SelectedDate = null;
        _appliedStartDate = null;
        _appliedEndDate = null;

        UpdateButtonText();
        _popup.IsOpen = false;

        DateRangeChanged?.Invoke(this, new DateRangeChangedEventArgs(null, null));
    }

    private void UpdateButtonText()
    {
        if (_appliedStartDate.HasValue || _appliedEndDate.HasValue)
        {
            var text = "📅 ";
            if (_appliedStartDate.HasValue && _appliedEndDate.HasValue)
                text += $"{_appliedStartDate.Value:dd/MM} - {_appliedEndDate.Value:dd/MM}";
            else if (_appliedStartDate.HasValue)
                text += $"≥ {_appliedStartDate.Value:dd/MM}";
            else if (_appliedEndDate.HasValue)
                text += $"≤ {_appliedEndDate.Value:dd/MM}";

            _filterButton.Content = text;
            _filterButton.FontSize = 11;
        }
        else
        {
            _filterButton.Content = "📅";
            _filterButton.FontSize = 16;
        }
    }

    public void ClearDates()
    {
        _startCalendar.SelectedDate = null;
        _endCalendar.SelectedDate = null;
        _appliedStartDate = null;
        _appliedEndDate = null;
        UpdateButtonText();
    }
}

public class DateRangeChangedEventArgs : EventArgs
{
    public DateTime? StartDate { get; }
    public DateTime? EndDate { get; }

    public DateRangeChangedEventArgs(DateTime? startDate, DateTime? endDate)
    {
        StartDate = startDate;
        EndDate = endDate;
    }
}
