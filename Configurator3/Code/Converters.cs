using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Globalization;

namespace Configurator.Code
{
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class InvertableBooleanToVisibilityConverter : IValueConverter 
    { 
        enum Parameters { Normal, Inverted }
 
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) 
        { 
            var b = (bool)value; 
            var direction = parameter == null ? Parameters.Normal : (Parameters)Enum.Parse(typeof(Parameters), (string)parameter); 
            if (direction == Parameters.Inverted)             
                return b ? Visibility.Collapsed : Visibility.Visible; 
            return b ? Visibility.Visible : Visibility.Collapsed; 
        } 
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) 
        { 
            return null; 
        } 
    }
}
