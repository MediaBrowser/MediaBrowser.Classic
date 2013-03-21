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
using MediaBrowser.Library.Network;

namespace Configurator {
    /// <summary>
    /// Interaction logic for AddPodcastForm.xaml
    /// </summary>
    public partial class AddPodcastForm : Window {

        public AddPodcastForm() {
            InitializeComponent();
        }

        public RSSFeed RSSFeed { get; private set; } 

        private void okButton_Click(object sender, RoutedEventArgs e) {
            try { 
                ValidatePodcast(podcastAddress.Text);
                this.DialogResult = true;
                this.Close();
            }
            catch(Exception ex) { 
                MessageBox.Show("Sorry, it seems the podcast you entered is either empty or invalid! " + ex.ToString()); 
            }
        }

        private void ValidatePodcast(string address) {
            RSSFeed = new RSSFeed(address);
            RSSFeed.Refresh();
        }

        private void button2_Click(object sender, RoutedEventArgs e) {
            this.Close();
        } 
    }
}
