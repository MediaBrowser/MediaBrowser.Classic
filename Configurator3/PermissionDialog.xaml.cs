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
using MediaBrowser.Library.Threading;
using System.Threading;
using System.Windows.Threading;
using System.Windows.Forms;

namespace Configurator
{
    /// <summary>
    /// Interaction logic for PermissionDialog.xaml
    /// </summary>
    public partial class PermissionDialog : Window
    {
        public PermissionDialog()
        {
            InitializeComponent();
            FakeProgress(progress, this);

        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            StopFakeProgress();
            base.OnClosing(e);
        }

        ManualResetEvent done = new ManualResetEvent(false);
        ManualResetEvent exited = new ManualResetEvent(false);

        private void StopFakeProgress()
        {
            done.Set();
            while (!exited.WaitOne()) ;
        }

        private void FakeProgress(System.Windows.Controls.ProgressBar progress, Window window)
        {
            Async.Queue("Fake progress for download", () =>
            {
                int i = 0;
                while (!done.WaitOne(500, false))
                {
                    i += 5;
                    i = i % 100;
                    window.Dispatcher.Invoke(DispatcherPriority.Background, (MethodInvoker)(() => { progress.Value = i; }));
                }
                exited.Set();
            });
        }

    }
}
