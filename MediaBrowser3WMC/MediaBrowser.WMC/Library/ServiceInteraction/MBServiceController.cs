using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Security.AccessControl;
using System.Security.Principal;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Configuration;
using MediaBrowser.Library.Threading;
using System.IO.Pipes;
using System.IO;

namespace MediaBrowser.Library
{
    public class MBServiceController
    {
        private static bool connected = false;
        public const string MBSERVICE_IN_PIPE = "{2A01C6A9-5244-45cb-B57E-7EFCED93766E}";
        public const string MBSERVICE_OUT_PIPE = "{8B012A2C-3920-4ab1-A685-A1F1A5C3BE6F}";

        /// <summary>
        /// Connect from core to the service - used in core to listen for commands from the service
        /// </summary>
        public static void ConnectToService()
        {
            if (connected) return; //only one connection...

            Async.Queue("MBService Connection", () =>
            {
                using (NamedPipeServerStream pipe = new NamedPipeServerStream(MBSERVICE_OUT_PIPE,PipeDirection.In))
                {
                    connected = true;
                    bool process = true;
                    while (process)
                    {
                        pipe.WaitForConnection(); //wait for the service to tell us something
                        try
                        {
                            // Read the request from the service.
                            StreamReader sr = new StreamReader(pipe);

                            string command = sr.ReadLine();
                            switch (command.ToLower())
                            {
                                case IPCCommands.ReloadItems:
                                    //refresh just finished, we need to re-load everything
                                    Logger.ReportInfo("Re-loading due to request from service.");
                                    Application.CurrentInstance.ReLoad();
                                    break;
                                case IPCCommands.Shutdown:
                                    //close MB
                                    Logger.ReportInfo("Shutting down due to request from service.");
                                    Application.CurrentInstance.Close();
                                    break;
                                case IPCCommands.CloseConnection:
                                    //exit this connection
                                    Logger.ReportInfo("Service requested we stop listening.");
                                    process = false;
                                    break;

                            }
                            pipe.Disconnect();
                        }
                        catch (IOException e)
                        {
                            Logger.ReportException("Error in MBService connection", e);
                        }
                    }
                    pipe.Close();
                    connected = false;
                }
            });
        }


        public static bool SendCommandToCore(string command) {
            return SendCommand(MBSERVICE_OUT_PIPE, command);
        }

        public static bool SendCommandToService(string command)
        {
            return SendCommand(MBSERVICE_IN_PIPE, command);
        }

        private static bool SendCommand(string pipeName, string command)
        {
            try
            {
                NamedPipeClientStream pipeClient =
                    new NamedPipeClientStream("localhost", pipeName,
                    PipeDirection.Out, PipeOptions.None);
                StreamWriter sw = new StreamWriter(pipeClient);
                try
                {
                    pipeClient.Connect(2000);
                }
                catch (TimeoutException)
                {
                    Logger.ReportInfo("Unable to send command (may not be running).");
                    return false;
                }
                catch (Exception e)
                {
                Logger.ReportException("Unable to connect to pipe: " + pipeName, e);
                Logger.ReportException("Inner Exception", e.InnerException);
                return false;
                }
                try
                {
                    sw.AutoFlush = true;
                    sw.WriteLine(command);
                    pipeClient.Close();
                }
                catch (Exception e)
                {
                    Logger.ReportException("Error sending commmand ", e);
                    return false;
                }
                return true;
            }
            catch (Exception e)
            {
                Logger.ReportException("Unable to open pipe: " + pipeName, e);
                Logger.ReportException("Inner Exception", e.InnerException);
                return false;
            }
        }

        public static bool IsRunning
        {
            get
            {
                using (Mutex mutex = new Mutex(false, Kernel.MBSERVICE_MUTEX_ID))
                {
                    //set up so everyone can access
                    var allowEveryoneRule = new MutexAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), MutexRights.FullControl, AccessControlType.Allow);
                    var securitySettings = new MutexSecurity();
                    try
                    {
                        //don't bomb if this fails
                        securitySettings.AddAccessRule(allowEveryoneRule);
                        mutex.SetAccessControl(securitySettings);
                    }
                    catch (Exception e)
                    {
                        //just log the exception and go on
                        Logger.ReportException("Failed setting access rule for mutex.", e);
                    }
                    try
                    {
                        return !(mutex.WaitOne(5000, false));
                    }
                    catch (AbandonedMutexException)
                    {
                        // Log the fact the mutex was abandoned in another process, it will still get acquired
                        Logger.ReportWarning("Previous instance of service ended abnormally...");
                        mutex.ReleaseMutex();
                        return false;
                    }
                }
            }
        }

        public static bool RestartService()
        {
            return SendCommandToService(IPCCommands.Restart);
        }

        public static bool StartService()
        {
            try
            {
                Logger.ReportInfo("Starting Service: " + ApplicationPaths.ServiceExecutableFile);
                System.Diagnostics.Process.Start(ApplicationPaths.ServiceExecutableFile);
            }
            catch (Exception e)
            {
                Logger.ReportError("Error attempting to start service. " + e.Message);
                return false;
            }
            return true;
        }
        public static bool StopService()
        {
            throw new NotImplementedException();
        }
    }
}
