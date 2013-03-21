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
using MediaBrowser.Library;

namespace Configurator
{
    /// <summary>
    /// Interaction logic for SelectMediaTypeForm.xaml
    /// </summary>
    public partial class SelectMediaTypeForm : Window
    {
        public SelectMediaTypeForm(List<MediaType> mediaTypes)
        {
            InitializeComponent();

            // Display the provided media types
            foreach (MediaType item in Enum.GetValues(typeof(MediaType)))
                if (mediaTypes.Contains(item))
                    this.cbMediaType.Items.Add(item);

            if (this.cbMediaType.Items.Count > 0)
                this.cbMediaType.SelectedIndex = 0;
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
