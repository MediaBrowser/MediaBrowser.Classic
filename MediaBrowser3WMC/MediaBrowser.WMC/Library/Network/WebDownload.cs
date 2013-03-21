using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Windows;


namespace MediaBrowser.Library.Network.WebDownload
{
    // Callbacks to allow updating GUI during transfer or upon completion
    public delegate void PluginInstallUpdateCB(double pctComplete);
    public delegate void PluginInstallFinishCB(); 
    public delegate void PluginInstallErrorCB(WebException ex);

    //public delegate void ResponseInfoDelegate(string statusDescr, string contentLength);
    //public delegate void ProgressDelegate(int totalBytes, double pctComplete, double transferRate);
    //public delegate void DoneDelegate();
    // public delegate void PluginInstallStartCB(string statusDescr, string contentLength);
    

    /// <summary>
    /// State object for HTTP transfers, that gets passed around amongst async methods 
    /// when doing async web request/response for data transfer.  We store basic 
    /// things that track current state of a download, including # bytes transfered,
    /// as well as some async callbacks that will get invoked at various points.
    /// </summary>
    public class State
    {
        public int bytesRead;           // # bytes read during current transfer
        public long totalBytes;		    // Total bytes to read
        public Stream streamResponse;	// Stream to read from 
        public byte[] bufferRead;	    // Buffer to read data into
        public FileStream downloadDest; // FileStream to write downloaded Data to
        public Uri fileURI;		        // Uri of object being downloaded
        
        // Callbacks for response packet info & progress
        public PluginInstallUpdateCB progCB;
        public PluginInstallFinishCB doneCB;
        public PluginInstallErrorCB errorCB;

        private HttpWebRequest _request;
        public WebRequest request
        {
            get
            {
                return _request;
            }
            set
            {
                _request = (HttpWebRequest)value;
            }
        }

        private HttpWebResponse _response;
        public WebResponse response
        {
            get
            {
                return _response;
            }
            set
            {
                _response = (HttpWebResponse)value;
            }
        }


        public State(int buffSize, string filePath)
        {
            bytesRead = 0;
            bufferRead = new byte[buffSize];
            streamResponse = null;
            downloadDest = new FileStream(filePath, FileMode.Create);
        }
    }

}

