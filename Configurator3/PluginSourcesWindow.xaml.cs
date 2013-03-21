using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Configurator.Properties;
using System.Collections.ObjectModel;
using Configurator.Code;
using MediaBrowser.Library.Plugins;
using MediaBrowser.Library;
using MediaBrowser;

namespace Configurator {

    /// <summary>
    /// Interaction logic for PluginSourcesWindow.xaml
    /// </summary>
    public partial class PluginSourcesWindow : Window {


        public PluginSourcesWindow() {
            InitializeComponent();
            sourceList.ItemsSource = PluginManager.Instance.Sources;
        }


        private void addButton_Click(object sender, RoutedEventArgs e) {
            var window = new AddPluginSourceWindow();
            var result = window.ShowDialog();

            if (result != null && result.Value && window.pluginSource.Text.Length > 0) {
                PluginManager.Instance.Sources.Add(window.pluginSource.Text);
            }

            PluginManager.Instance.RefreshAvailablePlugins();
       
        }

        private void removeButton_Click(object sender, RoutedEventArgs e) {
            PluginManager.Instance.Sources.Remove(sourceList.SelectedItem as string);
        }

        private void btnDone_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
