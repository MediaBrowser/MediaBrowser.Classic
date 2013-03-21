using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
//using System.Windows.Forms;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Configurator.Code;
using MediaBrowser.Library.Plugins;
using MediaBrowser.Library.Threading;
using System.Threading;
using System.Windows.Threading;
using System.Net;

namespace Configurator.Code
{
    public class PluginInstaller
    {
        ManualResetEvent done = new ManualResetEvent(false);
        ManualResetEvent exited = new ManualResetEvent(false);

        private System.Windows.Controls.ProgressBar m_progress;
        private Window m_window;
        private Delegate m_done;

        public void InstallPlugin(IPlugin plugin, System.Windows.Controls.ProgressBar progress, Window window, Delegate done) {
            if (plugin != null) {
                m_window = window;
                m_progress = progress;
                m_done = done;

                m_progress.Visibility = Visibility.Visible;

                MediaBrowser.Library.Network.WebDownload.PluginInstallUpdateCB updateDelegate = new MediaBrowser.Library.Network.WebDownload.PluginInstallUpdateCB(PluginInstallUpdate);
                MediaBrowser.Library.Network.WebDownload.PluginInstallFinishCB doneDelegate = new MediaBrowser.Library.Network.WebDownload.PluginInstallFinishCB(PluginInstallFinish);
                MediaBrowser.Library.Network.WebDownload.PluginInstallErrorCB errorDelegate = new MediaBrowser.Library.Network.WebDownload.PluginInstallErrorCB(PluginInstallError);
                
                PluginManager.Instance.InstallPlugin(plugin, updateDelegate, doneDelegate, errorDelegate);
            }
        }

        private void PluginInstallUpdate(double pctComplete) {
            if (m_progress.Dispatcher.CheckAccess()) {
                m_progress.Value = (int)pctComplete;
            }
            else {
                m_progress.Dispatcher.Invoke(new MediaBrowser.Library.Network.WebDownload.PluginInstallUpdateCB(this.PluginInstallUpdate), pctComplete);
            }
        }

        private void PluginInstallFinish() {
            if (m_progress.Dispatcher.CheckAccess()) {
                m_window.Dispatcher.Invoke(m_done, DispatcherPriority.Background);
            }
            else {
                m_progress.Dispatcher.Invoke(new MediaBrowser.Library.Network.WebDownload.PluginInstallFinishCB(this.PluginInstallFinish));
            }
        }

        private void PluginInstallError(WebException ex) {
            if (m_progress.Dispatcher.CheckAccess()) {
                MessageBox.Show(string.Format("Error while downloading plugin: {0}", ex.Message));
                // and make UI useable again
                m_window.Dispatcher.Invoke(m_done, DispatcherPriority.Background);
            }
            else {
                m_progress.Dispatcher.Invoke(new MediaBrowser.Library.Network.WebDownload.PluginInstallErrorCB(this.PluginInstallError), ex);
            }
        }
    }
}
