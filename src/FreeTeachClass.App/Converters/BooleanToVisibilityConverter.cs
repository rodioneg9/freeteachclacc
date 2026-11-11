using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace FreeTeachClass.App;

/// <summary>
/// Boolean → Visible/Collapsed. The <c>Invert</c> property flips the result
/// (used for "show this when the flag is false" cases).
/// </summary>
public sealed partial class BooleanToVisibilityConverter : IValueConverter
{
    /// <summary>Inverts the conversion result.</summary>
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var visible = value is bool flag && flag;
        if (Invert)
        {
            visible = !visible;
        }

        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
