using Avalonia.Data.Converters;
using Avalonia.Layout;
using System;
using System.Globalization;

namespace Office_Supplies_Inventory;
public class BoolToWidthConverter: IValueConverter {
    public object ? Convert(object ? value, Type targetType, object ? parameter, CultureInfo culture) {
        return (bool)(value ?? true) ? 280.0 : 95.0;
    }
    public object ? ConvertBack(object ? value, Type targetType, object ? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
public class BoolToOrientationConverter: IValueConverter {
    public object ? Convert(object ? value, Type targetType, object ? parameter, CultureInfo culture) {
        // If expanded (true), return Horizontal. If collapsed (false), return Vertical.
        return (bool)(value ?? true) ? Orientation.Horizontal : Orientation.Vertical;
    }

    public object ? ConvertBack(object ? value, Type targetType, object ? parameter, CultureInfo culture) {
        throw new NotImplementedException();
    }
}
public class BoolToColumnsConverter: IValueConverter {
    public object ? Convert(object ? value, Type targetType, object ? parameter, CultureInfo culture) {
        return (bool)(value ?? true) ? 2 : 1;
    }
    public object ? ConvertBack(object ? value, Type targetType, object ? parameter, CultureInfo culture) {
        throw new NotImplementedException();
    }
}
public class BoolToRowsConverter: IValueConverter {
    public object ? Convert(object ? value, Type targetType, object ? parameter, CultureInfo culture) {
        return (bool)(value ?? true) ? 1 : 2;
    }
    public object ? ConvertBack(object ? value, Type targetType, object ? parameter, CultureInfo culture) {
        throw new NotImplementedException();
    }
}