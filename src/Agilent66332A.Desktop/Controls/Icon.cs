using System.Windows;
using System.Windows.Media;

namespace Agilent66332A.Desktop.Controls;

public static class Icon
{
    public static readonly DependencyProperty GeometryProperty = DependencyProperty.RegisterAttached(
        "Geometry", typeof(Geometry), typeof(Icon), new PropertyMetadata(null));

    public static Geometry? GetGeometry(DependencyObject element) => (Geometry?)element.GetValue(GeometryProperty);
    public static void SetGeometry(DependencyObject element, Geometry? value) => element.SetValue(GeometryProperty, value);
}
