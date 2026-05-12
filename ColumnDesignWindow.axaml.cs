using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Collections.ObjectModel;
using System.Linq;

namespace Predator;

public partial class ColumnDesignWindow : Window
{
    private ListBox? _columnsList;
    public ObservableCollection<ColumnConfig> ColumnConfigs { get; }

    public ColumnDesignWindow()
    {
        InitializeComponent();
        ColumnConfigs = new ObservableCollection<ColumnConfig>();
        DataContext = this;
    }

    public ColumnDesignWindow(ObservableCollection<ColumnConfig> columns) : this()
    {
        ColumnConfigs.Clear();
        foreach (var col in columns.OrderBy(c => c.DisplayOrder))
        {
            ColumnConfigs.Add(new ColumnConfig(col.Header, col.PropertyName, col.DisplayOrder, col.IsVisible));
        }
        
        // Set ItemsSource after populating the collection
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _columnsList = this.FindControl<ListBox>("ColumnsList");
            if (_columnsList != null)
            {
                _columnsList.ItemsSource = ColumnConfigs;
            }
        });
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void OnMoveUpClick(object? sender, RoutedEventArgs e)
    {
        // Get the column config from the button's DataContext
        if (sender is not Button btn || btn.DataContext is not ColumnConfig current)
            return;

        // Auto-select this item if not already selected
        if (_columnsList != null)
            _columnsList.SelectedItem = current;

        var currentIndex = ColumnConfigs.IndexOf(current);
        if (currentIndex <= 0)
            return;

        // Intercambiar posiciones
        ColumnConfigs.RemoveAt(currentIndex);
        ColumnConfigs.Insert(currentIndex - 1, current);
        
        // Actualizar DisplayOrder basado en la nueva posición
        for (int i = 0; i < ColumnConfigs.Count; i++)
        {
            ColumnConfigs[i].DisplayOrder = i;
        }
        
        if (_columnsList != null)
            _columnsList.SelectedItem = current;
    }

    private void OnMoveDownClick(object? sender, RoutedEventArgs e)
    {
        // Get the column config from the button's DataContext
        if (sender is not Button btn || btn.DataContext is not ColumnConfig current)
            return;

        // Auto-select this item if not already selected
        if (_columnsList != null)
            _columnsList.SelectedItem = current;

        var currentIndex = ColumnConfigs.IndexOf(current);
        if (currentIndex >= ColumnConfigs.Count - 1)
            return;

        // Intercambiar posiciones
        ColumnConfigs.RemoveAt(currentIndex);
        ColumnConfigs.Insert(currentIndex + 1, current);
        
        // Actualizar DisplayOrder basado en la nueva posición
        for (int i = 0; i < ColumnConfigs.Count; i++)
        {
            ColumnConfigs[i].DisplayOrder = i;
        }
        
        if (_columnsList != null)
            _columnsList.SelectedItem = current;
    }
}
