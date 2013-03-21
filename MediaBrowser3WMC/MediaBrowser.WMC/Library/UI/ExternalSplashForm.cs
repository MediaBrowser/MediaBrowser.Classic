using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using MediaBrowser.Library.Threading;
using MediaBrowser.Library.Logging;
using System.Drawing;

namespace MediaBrowser.Library
{
    public class ExternalSplashForm
    {
        private delegate void VoidDelegate();
        static Form theForm;

        public static void Display(Bitmap bgImage)
        {
            var us = new ExternalSplashForm();
            Async.Queue("Ext Splash Show", () =>
            {
                Logger.ReportVerbose("Displaying Splash Screen");
                us.Show(bgImage);
            });
        }

        public static void Hide() {
            if (theForm != null)
            {
                theForm.Invoke(new VoidDelegate(delegate()
                    {
                        theForm.Close();
                    }));
            }
        }
        private static int retried = 0;
        public static void Activate()
        {
            if (theForm != null)
            {
                if (retried < 3)
                {
                    try
                    {
                        theForm.Invoke(new VoidDelegate(delegate()
                        {
                            theForm.Activate();
                            retried = 0;
                        }));
                    }
                    catch {
                        //probably wasn't up - try again
                        retried++;
                        Activate();
                    } 
                }
            }
        }

        void Show(Bitmap bgImage)
        {
            try
            {
                theForm = new Form();
                theForm.BackColor = Color.Black;
                theForm.BackgroundImageLayout = ImageLayout.Center;
                theForm.BackgroundImage = bgImage;
                theForm.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
                theForm.WindowState = System.Windows.Forms.FormWindowState.Maximized;
                Cursor.Hide();
                System.Windows.Forms.Application.Run(theForm);
            }
            catch (Exception e)
            {
                Logger.ReportException("Error showing external player splash form", e);
            }
        }

        

    }
}
