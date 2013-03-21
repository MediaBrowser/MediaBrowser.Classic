using System;
using System.IO;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Navigation;

namespace Configurator
{
	public partial class AddPluginSourceWindow
	{
		public AddPluginSourceWindow()
		{
			this.InitializeComponent();
            pluginSource.Focus();
			
			// Insert code required on object creation below this point.
		}

        private void btnOK_Click(object sender, RoutedEventArgs e) {
            this.DialogResult = true;
            this.Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e) {
            this.Close();
        }
	}
}