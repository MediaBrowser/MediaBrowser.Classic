using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Configurator {
    /// <summary>
    /// Interaction logic for AddExtenderFormat.xaml
    /// </summary>
    public partial class AddExtenderFormat : Window {
        public AddExtenderFormat() {
            InitializeComponent();
        }

        private void ok_Click(object sender, RoutedEventArgs e) {
            this.DialogResult = true;
            this.Close();
        }

        private void cancel_Click(object sender, RoutedEventArgs e) {
            this.Close();
        }
    }
}
