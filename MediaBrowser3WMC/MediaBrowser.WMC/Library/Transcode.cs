using System;
using System.Collections;
using System.Reflection;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Remoting.Channels;
using System.IO;
using Microsoft.Win32;
using MediaBrowser.Library.Logging;

namespace MediaBrowser.Library
{
    class Transcoder
    {
        public Transcoder()
        {
            // first, we need to identify where Transcode 360 was installed (if at all)
            RegistryKey key = Registry.LocalMachine;
            key = key.OpenSubKey(@"SOFTWARE\Transcode360");
            if (key == null) { return; }

            object installPath = key.GetValue("InstallPath");
            key.Close();
            if (installPath == null) { return; }
            InstallPath = installPath.ToString();

            // now that we have an installation path, let's make sure the dll is available
            if (File.Exists(InstallPath + "\\Transcode360.Interface.dll"))
            {
               
                ITranscode360 = LoadTranscode360(installPath + "\\Transcode360.Interface.dll");
                if (ITranscode360 == null) { return; }
                Hashtable properties = new Hashtable();
                properties.Add("name", "");
                Channel = new TcpClientChannel(properties, null);
                ChannelServices.RegisterChannel(Channel, false);

                Server = Activator.GetObject(ITranscode360,
                   "tcp://localhost:1401/RemotingServices/Transcode360");
                
        
                ParameterModifier[] arrPmods = new ParameterModifier[1];
                arrPmods[0] = new ParameterModifier(1);
                arrPmods[0][0] = false; // not out
                
                System.Type[] arrTypes = new System.Type[1];
                arrTypes.SetValue(Type.GetType("System.String"),0);

                methStopTranscoding = ITranscode360.GetMethod("StopTranscoding",
                    arrTypes,
                    arrPmods);          

                // IsMediaTranscodeComplete
                arrPmods[0] = new ParameterModifier(3);
                arrPmods[0][0] = false; // not out
                arrPmods[0][1] = false;
                arrPmods[0][2] = true;

                arrTypes = new System.Type[3];
                arrTypes.SetValue(Type.GetType("System.String"), 0);
                arrTypes.SetValue(Type.GetType("System.Int64"), 1);
                arrTypes.SetValue(Type.GetType("System.String&"), 2);
                
                methIsMediaTranscodeComplete = ITranscode360.GetMethod("IsMediaTranscodeComplete",
                    arrTypes,
                    arrPmods);

                // IsMediaTranscoding
                arrPmods[0] = new ParameterModifier(3);
                arrPmods[0][0] = false; // not out
                arrPmods[0][1] = false;
                arrPmods[0][2] = true;

                arrTypes = new System.Type[3];
                arrTypes.SetValue(Type.GetType("System.String"), 0);
                arrTypes.SetValue(Type.GetType("System.Int64"), 1);
                arrTypes.SetValue(Type.GetType("System.String&"), 2);

                methIsMediaTranscoding = ITranscode360.GetMethod("IsMediaTranscoding",
                    arrTypes,
                    arrPmods);

                // Transcode
                arrPmods[0] = new ParameterModifier(3);
                arrPmods[0][0] = false; // not out
                arrPmods[0][1] = true;
                arrPmods[0][2] = false;
                
                arrTypes = new System.Type[3];
                arrTypes.SetValue(Type.GetType("System.String"), 0);
                arrTypes.SetValue(Type.GetType("System.String&"), 1);
                arrTypes.SetValue(Type.GetType("System.Int64"), 2);

                methTranscode = ITranscode360.GetMethod("Transcode",
                    arrTypes,
                    arrPmods);
             }
        }
        
        public String InstallPath { get; private set; }
        private Object Server = null;
        private Type ITranscode360 = null;
        private TcpClientChannel Channel = null;
        private MethodInfo methStopTranscoding = null;
        private MethodInfo methIsMediaTranscodeComplete = null;
        private MethodInfo methIsMediaTranscoding = null;
        private MethodInfo methTranscode = null;
        
        private Type LoadTranscode360(string dllPath) 
        {
            try
            {
                Assembly asm = Assembly.LoadFrom(dllPath);
                Type[] types = asm.GetTypes();
                foreach (Type type in asm.GetTypes())
                {
                    if (type.Name.CompareTo(@"ITranscode360") == 0)
                    {
                        return type;
                    }
                }
                
            }
            catch (Exception e)
            {
               Logger.ReportException("Unable to load Transcode360", e);
            }   
            return null;
        }
       
        public string BeginTranscode(string filename)
        {
            bool result = false;
            
            if (ITranscode360 == null || Server == null) 
            {
                return null;
            }

            // Check if the transcode is already completed
            object[] arrParms = new object[3];
            arrParms.SetValue(filename, 0);
            arrParms.SetValue(0, 1);
            arrParms.SetValue(null, 2);
            result = (bool)methIsMediaTranscodeComplete.Invoke(Server, arrParms);
            if (result == true) 
            {
                return (string)arrParms[2];
            }

            // the file is already being transcoded (or is already done)
            arrParms = new object[3];
            arrParms.SetValue(filename, 0);
            arrParms.SetValue(0, 1);
            arrParms.SetValue(null, 2);
            result = (bool)methIsMediaTranscoding.Invoke(Server, arrParms);
            if (result == true)
            {
                return (string)arrParms[2];
            }
            
            // Otherwise we need to start the transcode
            arrParms = new object[3];
            arrParms.SetValue(filename, 0);
            arrParms.SetValue(null, 1);
            arrParms.SetValue(0, 2);
            result = (bool)methTranscode.Invoke(Server, arrParms);
            if (result == true)
            {
                return (string)arrParms[1];
            }
            return null;
        }
    }
}