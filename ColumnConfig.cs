using System.ComponentModel;

namespace Predator;

public sealed class ColumnConfig : INotifyPropertyChanged
{
    private bool _isVisible;
    private int _displayOrder;

    public string Header { get; init; }
    public string PropertyName { get; init; }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible != value)
            {
                _isVisible = value;
                OnPropertyChanged(nameof(IsVisible));
            }
        }
    }

    public int DisplayOrder
    {
        get => _displayOrder;
        set
        {
            if (_displayOrder != value)
            {
                _displayOrder = value;
                OnPropertyChanged(nameof(DisplayOrder));
            }
        }
    }

    public ColumnConfig(string header, string propertyName, int displayOrder, bool isVisible = true)
    {
        Header = header;
        PropertyName = propertyName;
        DisplayOrder = displayOrder;
        _isVisible = isVisible;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
