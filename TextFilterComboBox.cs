using Avalonia.Controls;
using System;

namespace Predator;

public sealed class TextFilterComboBox : ComboBox
{
    protected override Type StyleKeyOverride => typeof(ComboBox);

    public TextFilterComboBox()
    {
        IsEditable = true;
    }
}
