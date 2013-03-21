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
    /// Interaction logic for WarnDialog.xaml
    /// </summary>
    public partial class SuppImproveDialog : Window
    {
        public SuppImproveDialog()
        {
            InitializeComponent();
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            Kernel.Instance.ConfigData.SendStats = true;
            Kernel.Instance.ConfigData.Save();
            MainWindow.Instance.cbxSendStats.IsChecked = true;
            this.Close();
        }

        public static bool Show(string msg)
        {
            return Show(msg, true);
        }

        public static bool Show(string msg, bool allowDontShow)
        {
            SuppImproveDialog dlg = new SuppImproveDialog();
            dlg.tbMessage.Text = msg;
            dlg.cbxDontShowAgain.Visibility = allowDontShow ? Visibility.Visible : Visibility.Hidden;
            dlg.ShowDialog();
            return dlg.cbxDontShowAgain.IsChecked.Value;
        }
    }
}
