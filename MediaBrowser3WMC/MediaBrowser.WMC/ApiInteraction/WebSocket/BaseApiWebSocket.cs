using System.IO;
using MediaBrowser.Library;
using MediaBrowser.Library.Extensions;
using MediaBrowser.Library.Logging;
using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Events;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Net;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Session;
using MediaBrowser.Model.Tasks;
using MediaBrowser.Model.Updates;
using System;
using System.Text;

namespace MediaBrowser.ApiInteraction.WebSocket
{
    /// <summary>
    /// Class ApiWebSocket
    /// </summary>
    public abstract class BaseApiWebSocket
    {
        /// <summary>
        /// The _json serializer
        /// </summary>
        private readonly IJsonSerializer _jsonSerializer;
        /// <summary>
        /// Occurs when [user deleted].
        /// </summary>
        public event EventHandler<UserDeletedEventArgs> UserDeleted;
        /// <summary>
        /// Occurs when [scheduled task started].
        /// </summary>
        public event EventHandler<ScheduledTaskStartedEventArgs> ScheduledTaskStarted;
        /// <summary>
        /// Occurs when [scheduled task ended].
        /// </summary>
        public event EventHandler<ScheduledTaskEndedEventArgs> ScheduledTaskEnded;
        /// <summary>
        /// Occurs when [package installing].
        /// </summary>
        public event EventHandler<PackageInstallationEventArgs> PackageInstalling;
        /// <summary>
        /// Occurs when [package installation failed].
        /// </summary>
        public event EventHandler<PackageInstallationEventArgs> PackageInstallationFailed;
        /// <summary>
        /// Occurs when [package installation completed].
        /// </summary>
        public event EventHandler<PackageInstallationEventArgs> PackageInstallationCompleted;
        /// <summary>
        /// Occurs when [package installation cancelled].
        /// </summary>
        public event EventHandler<PackageInstallationEventArgs> PackageInstallationCancelled;
        /// <summary>
        /// Occurs when [user updated].
        /// </summary>
        public event EventHandler<UserUpdatedEventArgs> UserUpdated;
        /// <summary>
        /// Occurs when [library changed].
        /// </summary>
        public event EventHandler<LibraryChangedEventArgs> LibraryChanged;

        public event EventHandler<BrowseRequestEventArgs> BrowseCommand;
        public event EventHandler<PlayRequestEventArgs> PlayCommand;
        public event EventHandler<PlaystateRequestEventArgs> PlaystateCommand;
        public event EventHandler<SystemRequestEventArgs> SystemCommand;
        public event EventHandler<GenericEventArgs<GeneralCommandEventArgs>> GeneralCommand;

        /// <summary>
        /// Occurs when [restart required].
        /// </summary>
        public event EventHandler<EventArgs> RestartRequired;

        /// <summary>
        /// The identification message name
        /// </summary>
        protected const string IdentificationMessageName = "Identity";

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiWebSocket" /> class.
        /// </summary>
        protected BaseApiWebSocket()
        {
            _jsonSerializer = new NewtonsoftJsonSerializer();
        }

        /// <summary>
        /// Gets the web socket URL.
        /// </summary>
        /// <param name="serverHostName">Name of the server host.</param>
        /// <param name="serverWebSocketPort">The server web socket port.</param>
        /// <returns>System.String.</returns>
        protected string GetWebSocketUrl(string serverHostName, int serverWebSocketPort)
        {
            return string.Format("ws://{0}:{1}/mediabrowser", serverHostName, serverWebSocketPort);
        }

        /// <summary>
        /// Called when [message received].
        /// </summary>
        /// <param name="json"></param>
        protected void OnMessageReceived(string json)
        {
            WebSocketMessage<object> message = null;

            try
            {
                message = _jsonSerializer.DeserializeFromString<WebSocketMessage<object>>(json);

                Logger.ReportInfo("Received web socket message: {0}", message.MessageType);
            }
            catch (Exception e)
            {
                Logger.ReportException("Error interpreting Websocket message",e);
                Logger.ReportError("Message received was {0}", json);
                return;
            }


            try
            {
                if (string.Equals(message.MessageType, "LibraryChanged"))
                {
                    FireEvent(LibraryChanged, this, new LibraryChangedEventArgs
                                                        {
                                                            UpdateInfo = _jsonSerializer.DeserializeFromString<LibraryUpdateInfo>(message.Data.ToString())
                                                        });
                }
                else if (string.Equals(message.MessageType, "RestartRequired"))
                {
                    FireEvent(RestartRequired, this, EventArgs.Empty);
                }
                else if (string.Equals(message.MessageType, "UserDeleted"))
                {
                    FireEvent(UserDeleted, this, new UserDeletedEventArgs
                                                     {
                                                         Id = message.Data.ToString()
                                                     });
                }
                else if (string.Equals(message.MessageType, "ScheduledTaskStarted"))
                {
                    FireEvent(ScheduledTaskStarted, this, new ScheduledTaskStartedEventArgs
                                                              {
                                                                  Name = message.Data.ToString()
                                                              });
                }
                else if (string.Equals(message.MessageType, "ScheduledTaskEnded"))
                {
                    FireEvent(ScheduledTaskEnded, this, new ScheduledTaskEndedEventArgs
                                                            {
                                                                Result = _jsonSerializer.DeserializeFromString<TaskResult>(message.Data.ToString())
                                                            });
                }
                else if (string.Equals(message.MessageType, "PackageInstalling"))
                {
                    FireEvent(PackageInstalling, this, new PackageInstallationEventArgs
                                                           {
                                                               InstallationInfo = _jsonSerializer.DeserializeFromString<InstallationInfo>(message.Data.ToString())
                                                           });
                }
                else if (string.Equals(message.MessageType, "PackageInstallationFailed"))
                {
                    FireEvent(PackageInstallationFailed, this, new PackageInstallationEventArgs
                                                                   {
                                                                       InstallationInfo = _jsonSerializer.DeserializeFromString<InstallationInfo>(message.Data.ToString())
                                                                   });
                }
                else if (string.Equals(message.MessageType, "PackageInstallationCompleted"))
                {
                    FireEvent(PackageInstallationCompleted, this, new PackageInstallationEventArgs
                                                                      {
                                                                          InstallationInfo = _jsonSerializer.DeserializeFromString<InstallationInfo>(message.Data.ToString())
                                                                      });
                }
                else if (string.Equals(message.MessageType, "PackageInstallationCancelled"))
                {
                    FireEvent(PackageInstallationCancelled, this, new PackageInstallationEventArgs
                                                                      {
                                                                          InstallationInfo = _jsonSerializer.DeserializeFromString<InstallationInfo>(message.Data.ToString())
                                                                      });
                }
                else if (string.Equals(message.MessageType, "UserUpdated"))
                {
                    FireEvent(UserUpdated, this, new UserUpdatedEventArgs
                                                     {
                                                         User = _jsonSerializer.DeserializeFromString<UserDto>(message.Data.ToString())
                                                     });
                }
                else if (string.Equals(message.MessageType, "Browse"))
                {
                    FireEvent(BrowseCommand, this, new BrowseRequestEventArgs
                                                       {
                                                           Request = _jsonSerializer.DeserializeFromString<BrowseRequest>(message.Data.ToString())
                                                       });
                }
                else if (string.Equals(message.MessageType, "Play"))
                {
                    FireEvent(PlayCommand, this, new PlayRequestEventArgs
                                                     {
                                                         Request = _jsonSerializer.DeserializeFromString<PlayRequest>(message.Data.ToString())
                                                     });
                }
                else if (string.Equals(message.MessageType, "Playstate"))
                {
                    FireEvent(PlaystateCommand, this, new PlaystateRequestEventArgs
                                                          {
                                                              Request = _jsonSerializer.DeserializeFromString<PlaystateRequest>(message.Data.ToString())
                                                          });
                }
                else if (string.Equals(message.MessageType, "SystemCommand"))
                {
                    FireEvent(SystemCommand, this, new SystemRequestEventArgs()
                                                       {
                                                           Command = message.Data.ToString()
                                                       });
                }
                else if (string.Equals(message.MessageType, "GeneralCommand"))
                {
                    if (GeneralCommand != null)
                    {
                        GeneralCommand(this, new GenericEventArgs<GeneralCommandEventArgs>(new GeneralCommandEventArgs()
                                                 {
                                                     Command = _jsonSerializer.DeserializeFromString<WebSocketMessage<GeneralCommand>>(json).Data
                                                 }));
                    }
                }
            }
            catch (Exception e)
            {
                Logger.ReportException("Error Interpreting Websocket message data",e);
                Logger.ReportError("Message data received was {0}",message.Data.ToString());
            }
        }

        /// <summary>
        /// Queues the event if not null.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="handler">The handler.</param>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The args.</param>
        private void FireEvent<T>(EventHandler<T> handler, object sender, T args)
            where T : EventArgs
        {
            if (handler != null)
            {
                try
                {
                    handler(sender, args);
                }
                catch (Exception ex)
                {
                    Logger.ReportException("Error in event handler", ex);
                }
            }
        }

        /// <summary>
        /// Gets the message bytes.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="messageName">Name of the message.</param>
        /// <param name="data">The data.</param>
        /// <returns>System.Byte[][].</returns>
        protected byte[] GetMessageBytes<T>(string messageName, T data)
        {
            var msg = new WebSocketMessage<T> { MessageType = messageName, Data = data };

            using (var stream = new MemoryStream())
            {
                _jsonSerializer.SerializeToStream(msg, stream);
                return stream.ToArray();
            }
        }

        /// <summary>
        /// Gets the identification message.
        /// </summary>
        /// <param name="clientName">Name of the client.</param>
        /// <param name="deviceName">The device id.</param>
        /// <returns>System.String.</returns>
        protected string GetIdentificationMessage(string clientName, string deviceName)
        {
            return clientName + "|" + Kernel.ApiClient.DeviceId + "|" + Kernel.Instance.VersionStr + "|" + deviceName;
        }
    }
}
