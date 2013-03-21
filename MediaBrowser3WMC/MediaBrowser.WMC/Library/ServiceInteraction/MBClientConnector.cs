using System;
using System.IO;
using System.IO.Pipes;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Threading;

namespace MediaBrowser.Library
{
    class MBClientConnector
    {

        private static bool connected = false;

        public static bool StartListening()
        {
            if (connected) return false; //only one connection...
            if (Application.RunningOnExtender)
            {
                Logger.ReportInfo("Running on an extender.  Not starting client listener.");
                return true; //no comms for extenders
            }

            NamedPipeServerStream pipe;
            try
            {
                pipe = new NamedPipeServerStream(Kernel.MBCLIENT_MUTEX_ID);
            }
            catch (IOException)
            {
                Logger.ReportInfo("Client listener already going - activating that instance of MB...");
                //already started - must be another instance of MB Core - tell it to come to front
                string entryPoint = EntryPointResolver.EntryPointPath;
                if (string.IsNullOrEmpty(entryPoint))
                {
                    SendCommandToCore("activate");
                }
                else //nav to the proper entrypoint
                {
                    Logger.ReportInfo("Navigating current instance to entrypoint " + entryPoint);
                    SendCommandToCore("activateentrypoint," + entryPoint);
                }
                //and exit
                return false;
            }

            connected = true;

            Async.Queue("MBClient Listener", () =>
            {

                bool process = true;
                while (process)
                {
                    pipe.WaitForConnection(); //wait for someone to tell us something
                    string[] commandAndArgs;
                    try
                    {
                        // Read the request from the client. 
                        StreamReader sr = new StreamReader(pipe);

                        commandAndArgs = sr.ReadLine().Split(',');
                    }
                    catch (Exception e)
                    {
                        Logger.ReportException("Error during IPC communication.  Attempting to re-start listener", e);
                        try
                        {
                            //be sure we're cleaned up
                            pipe.Disconnect();
                            pipe.Close();
                        }
                        catch
                        { //we don't care if these fail now - and they very well may
                        }
                        finally
                        {
                            connected = false;
                        }
                        StartListening();
                        return;
                    }
                    try
                    {
                        string command = commandAndArgs[0];
                        switch (command.ToLower())
                        {
                            case "play":
                                //request to play something - our argument will be the GUID of the item to play
                                Guid id = new Guid(commandAndArgs[1]);
                                Logger.ReportInfo("Playing ...");
                                //to be implemented...
                                break;
                            case "activateentrypoint":
                                //re-load ourselves and nav to the entrypoint
                                Kernel.Instance.ReLoadRoot();
                                Microsoft.MediaCenter.UI.Application.DeferredInvoke(_ =>
                                {
                                    MediaBrowser.Application.CurrentInstance.LaunchEntryPoint(commandAndArgs[1]);
                                });
                                //and tell MC to navigate to us
                                Microsoft.MediaCenter.Hosting.AddInHost.Current.ApplicationContext.ReturnToApplication();
                                break;
                            case "activate":
                                Logger.ReportInfo("Asked to activate by another process..");
                                //if we were in an entrypoint and we just got told to activate - we need to re-load and go to real root
                                if (Application.CurrentInstance.IsInEntryPoint)
                                {
                                    Kernel.Instance.ReLoadRoot();
                                    Microsoft.MediaCenter.UI.Application.DeferredInvoke(_ =>
                                    {
                                        MediaBrowser.Application.CurrentInstance.LaunchEntryPoint(""); //this will start at root
                                    });
                                }
                                else
                                {
                                    //just need to back up to the root
                                    Application.CurrentInstance.BackToRoot();
                                }

                                // set npv visibility according to current state
                                Application.CurrentInstance.ShowNowPlaying = Application.CurrentInstance.IsPlaying || Application.CurrentInstance.IsExternalWmcApplicationPlaying;

                                //tell MC to navigate to us
                                Microsoft.MediaCenter.Hosting.AddInHost.Current.ApplicationContext.ReturnToApplication();
                                break;
                            case "shutdown":
                                //close MB
                                Logger.ReportInfo("Shutting down due to request from a client (possibly new instance of MB).");
                                Application.CurrentInstance.Close();
                                break;
                            case "closeconnection":
                                //exit this connection
                                Logger.ReportInfo("Service requested we stop listening.");
                                process = false;
                                break;

                        }
                    }
                    catch (Exception e)
                    {
                        Logger.ReportException("Error trying to process IPC command", e);
                    }
                    try
                    {
                        pipe.Disconnect();
                    }
                    catch (Exception e)
                    {
                        Logger.ReportException("Unexpected Error trying to close IPC connection", e);
                    }
                }
                pipe.Close();
                connected = false;
            });
            return true;
        }

        public static bool SendCommandToCore(string command)
        {
            return SendCommandToCore("localhost", command);
        }

        public static bool SendCommandToCore(string machine, string command)
        {
            NamedPipeClientStream pipeClient =
                new NamedPipeClientStream(machine, Kernel.MBCLIENT_MUTEX_ID,
                PipeDirection.Out, PipeOptions.None);
            StreamWriter sw = new StreamWriter(pipeClient);
            try
            {
                pipeClient.Connect(2000);
            }
            catch (TimeoutException)
            {
                Logger.ReportWarning("Unable to send command to core (may not be running).");
                return false;
            }
            try
            {
                sw.AutoFlush = true;
                sw.WriteLine(command);
                pipeClient.Flush();
                pipeClient.Close();
            }
            catch (Exception e)
            {
                Logger.ReportException("Error sending commmand to core", e);
                return false;
            }
            return true;
        }
    }
}
