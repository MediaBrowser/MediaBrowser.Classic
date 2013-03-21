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
using System.ComponentModel;
using Configurator.Code;
using MediaBrowser.Library.Plugins;
using MediaBrowser.Library.Threading;
using MediaBrowser.Library;
using System.Windows.Forms;
using System.Threading;
using System.Windows.Threading;
using MediaBrowser.Library.Logging;

namespace Configurator {
    /// <summary>
    /// Interaction logic for AddPluginWindow.xaml
    /// </summary>
    public partial class AddPluginWindow : Window {
        public AddPluginWindow() {
            InitializeComponent();
            progress.Minimum = 0;
            progress.Maximum = 100;
            FilterPluginList();
        }

        private void PluginFilter(object sender, FilterEventArgs e)
        {
            var plugin = e.Item as IPlugin;
            e.Accepted = (plugin.IsLatestVersion || cbxShowAll.IsChecked.Value) && (!plugin.IsPremium || !cbxFreeOnly.IsChecked.Value);
        }

        private void FilterPluginList()
        {
            CollectionViewSource src = new CollectionViewSource();
            src.Source = PluginManager.Instance.AvailablePlugins;
            src.GroupDescriptions.Add(new PropertyGroupDescription("PluginClass"));
            src.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
            src.SortDescriptions.Add(new SortDescription("Version", ListSortDirection.Descending));
            if (cbxShowAll.IsChecked == true)
            {
                src.GroupDescriptions.Add(new PropertyGroupDescription("Name"));
            }
            src.Filter += PluginFilter;
            pluginList.ItemsSource = src.View;
            pluginList.SelectedItem = null; //since we're collapsed, come up with no selection
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e) {
            PluginSourcesWindow window = new PluginSourcesWindow();
            window.ShowDialog();
        }

        private void InstallClick(object sender, RoutedEventArgs e) {
            InstallButton.IsEnabled = false;
            btnDone.IsEnabled = false;
            pluginList.IsEnabled = false;
            this.progress.Visibility = Visibility.Visible;
            PluginInstaller p = new PluginInstaller();
            callBack done = new callBack(InstallFinished);
            p.InstallPlugin(pluginList.SelectedItem as IPlugin, progress, this, done);
            MainWindow.Instance.KernelModified = true;

        }

        private delegate void callBack();

        public void InstallFinished()
        {
            //called when the install is finished 
            InstallButton.IsEnabled = true;
            btnDone.IsEnabled = true;
            pluginList.IsEnabled = true;
            this.progress.Value = 0;
            this.progress.Visibility = Visibility.Hidden;
            IPlugin plugin = pluginList.SelectedItem as IPlugin;
            if (plugin != null)
            {
                updateAttributes(plugin);
                Logger.ReportInfo(plugin.Name + " v" + plugin.Version + " Installed.");
            }
        }

        private void updateAttributes(IPlugin plugin)
        {
            PluginManager.Instance.UpdateAvailableAttributes(plugin, true);
            pluginList.Items.Refresh();
        }

        private void btnDone_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void pluginList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //validate the required MB version for this plug-in
            if (e != null && pluginList.SelectedItem != null && InstallButton != null && MessageLine != null)
            {
                txtNoSelection.Visibility = Visibility.Hidden;
                lblVer.Visibility = lblReq.Visibility = Visibility.Visible;

                IPlugin plugin = pluginList.SelectedItem as IPlugin;
                System.Version rpv = new System.Version(0, 0, 0, 0);
                PluginManager.RequiredVersions.TryGetValue(plugin.Name.ToLower(), out rpv);
                if (plugin.RequiredMBVersion > Kernel.Instance.Version)
                {
                    InstallButton.IsEnabled = false;
                    MessageLine.Content = plugin.Name + " requires at least version " + plugin.RequiredMBVersion + ".  Current MB version installed is " + Kernel.Instance.Version;
                } else if (plugin.Version < rpv) {
                    InstallButton.IsEnabled = false;
                    MessageLine.Content = plugin.Name + " version " + plugin.Version + " is not compatible with this version of MB (" + Kernel.Instance.Version+")";
                }
                else
                {
                    InstallButton.IsEnabled = true;
                    MessageLine.Content = "";
                }
                if (RichDescFrame != null)
                {
                    if (!String.IsNullOrEmpty(plugin.RichDescURL))
                    {
                        RichDescFrame.Navigate(new Uri(plugin.RichDescURL, UriKind.Absolute));
                    }
                    else
                    {
                        RichDescFrame.Visibility = Visibility.Hidden;
                        
                    }
                }
                lblRegRequired.Visibility = plugin.IsPremium ? Visibility.Visible : Visibility.Hidden;

            }
            else // no selection
            {
                RichDescFrame.Visibility = lblReq.Visibility = lblVer.Visibility = Visibility.Hidden;
                txtNoSelection.Visibility = Visibility.Visible;
                lblRegRequired.Visibility = Visibility.Hidden;
                InstallButton.IsEnabled = false;
            }
        }

        private void RichDescFrame_NavigationFailed(object sender, System.Windows.Navigation.NavigationFailedEventArgs e)
        {
            Logger.ReportError("Navigation to Rich Description failed.  Error: "+e.Exception.Message);
            RichDescFrame.Visibility = Visibility.Hidden;
            e.Handled = true;
        }

        private void RichDescFrame_Navigating(object sender, System.Windows.Navigation.NavigatingCancelEventArgs e)
        {
            if (pluginList.SelectedItem != null)
            {
                IPlugin plugin = pluginList.SelectedItem as IPlugin;
                if (e.Uri != new Uri(plugin.RichDescURL, UriKind.Absolute))
                {
                    MessageLine.Content = "Cannot Follow Links";
                    e.Cancel = true; //don't allow navigating away from our main page
                }
            }
        }

        private void RichDescFrame_LoadCompleted(object sender, System.Windows.Navigation.NavigationEventArgs e)
        {
            mshtml.HTMLDocumentClass doc = (mshtml.HTMLDocumentClass)RichDescFrame.Document;
            if (doc.body.innerHTML.Contains("404:"))
            {
                Logger.ReportError("Rich Description Not Found.");
                RichDescFrame.Visibility = Visibility.Hidden;
            }
            else
            {
                if (pluginList.SelectedItem != null)
                    RichDescFrame.Visibility = Visibility.Visible;
            }
        }

        private void pluginList_Collapse(object sender, RoutedEventArgs e)
        {
            //un-select in case current item was collapsed from view
            pluginList.SelectedIndex = -1;

        }

        private void cbxShowAll_Checked(object sender, RoutedEventArgs e)
        {
            FilterPluginList();
        }

        private void cbxFreeOnly_Checked(object sender, RoutedEventArgs e)
        {
            FilterPluginList();
        }


    }
}
