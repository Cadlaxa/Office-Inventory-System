using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Office_Supplies_Inventory {
    public class StockColorConverter: IMultiValueConverter {
        public object Convert(IList < object ? > values, Type targetType, object ? parameter, CultureInfo culture) {
            bool isRow = parameter as string == "Row";

            if (values?.Count == 4 &&
                values[0] is IConvertible finalVal &&
                values[1] is IConvertible initialVal &&
                values[2] is IConvertible stockInVal &&
                values[3] is IConvertible stockOutVal) {
                double finalStock = finalVal.ToDouble(null);
                double initialStock = initialVal.ToDouble(null);
                double stockIn = stockInVal.ToDouble(null);
                double stockOut = stockOutVal.ToDouble(null);

                double totalBasePool = initialStock + stockIn;

                if (totalBasePool <= 0)
                    return isRow ? Brushes.Transparent : new SolidColorBrush(Color.Parse("#0F6CBD"));

                double calculatedPercentage = ((initialStock + stockIn - stockOut) / totalBasePool) * 100;

                // Drop opacity to 15% if it's a row highlight so text stays readable
                double opacity = isRow ? 0.15 : 1.0;

                if (calculatedPercentage <= 10)
                    return new SolidColorBrush(Color.Parse("#DC3545")) {
                        Opacity = opacity
                    }; // Red

                if (calculatedPercentage <= 30)
                    return new SolidColorBrush(Color.Parse("#E85D04")) {
                        Opacity = opacity
                    }; // Orange

                if (calculatedPercentage <= 60)
                    return new SolidColorBrush(Color.Parse("#D49600")) {
                        Opacity = opacity
                    }; // Yellow
            }

            // Default State: Blue for the badge, but Transparent for the row to keep default DataGrid styling
            return isRow ? Brushes.Transparent : new SolidColorBrush(Color.Parse("#0F6CBD"));
        }

        public object ConvertBack(IList < object ? > values, Type targetType, object ? parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}