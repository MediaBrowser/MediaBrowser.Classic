using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Xml;
using MediaBrowser.LibraryManagement;
using MediaBrowser.Library.Extensions;
using System.IO;
using MediaBrowser.Library.Logging;

namespace MediaBrowser.Library.Plugins {
    public class PluginSourceCollection : ObservableCollection<string> {

        public static PluginSourceCollection Instance = new PluginSourceCollection();

        private PluginSourceCollection() {
            foreach (var item in Config.Instance.PluginSources) {
                Items.Add(item);
            }
        }

        protected override void InsertItem(int index, string item) {
            base.InsertItem(index, item);
            Config.Instance.PluginSources = this.Items.ToList(); //cause config to update
        }

        protected override void RemoveItem(int index) {
            base.RemoveItem(index);
            Config.Instance.PluginSources = this.Items.ToList(); //cause config to update
        }

        public IEnumerable<IPlugin> AvailablePlugins {
            get {
                List<IPlugin> plugins = new List<IPlugin>();
                foreach (var source in this) {
                    plugins.AddRange(DiscoverPlugins(source));
                }
                return plugins;
            }
        }

        private List<IPlugin> DiscoverPlugins(string source) {
            if (source.ToLower().StartsWith("http")) {
                return DiscoverRemotePlugins(source);
            } else {
                return DiscoverLocalPlugins(source);
            }
        }

        private List<IPlugin> DiscoverLocalPlugins(string source) {
            var list = new List<IPlugin>();
            if (Directory.Exists(source))
            {
                foreach (var file in Directory.GetFiles(source))
                {
                    if (file.ToLower().EndsWith(".dll"))
                    {
                        try
                        {
                            list.Add(Plugin.FromFile(file, true));
                        }
                        catch (Exception e)
                        {
                            Logger.ReportException("Error attempting to load " + file + " as plug-in.", e);
                        }
                    }
                }
            }
            return list;
        }

        private List<IPlugin> DiscoverRemotePlugins(string source) {
            var list = new List<IPlugin>();
            XmlDocument doc = Helper.Fetch(source);
            if (doc != null) {
                foreach (XmlNode pluginRoot in doc.SelectNodes(@"Plugins//Plugin")) {
                    string installGlobally = pluginRoot.SafeGetString("InstallGlobally") ?? "false"; //get this safely in case its not there
                    string isPremium = pluginRoot.SafeGetString("IsPremium") ?? "false"; //get this safely in case its not there
                    string requiredVersion = pluginRoot.SafeGetString("RequiredMBVersion") ?? "2.0.0.0"; //get this safely in case its not there
                    string testedVersion = pluginRoot.SafeGetString("TestedMBVersion") ?? "2.0.0.0"; //get this safely in case its not there
                    string richURL = pluginRoot.SafeGetString("RichDescURL") ?? ""; //get this safely in case its not there
                    string assumedClass = installGlobally == "true" ? PluginClasses.Themes : PluginClasses.Other;

                    list.Add(new RemotePlugin()
                    {
                        Description = pluginRoot.SafeGetString("Description"),
                        RichDescURL = richURL,
                        Filename = pluginRoot.SafeGetString("Filename"),
                        SourceFilename = pluginRoot.SafeGetString("SourceFilename") ?? pluginRoot.SafeGetString("Filename"),
                        Version = new System.Version(pluginRoot.SafeGetString("Version")),
                        RequiredMBVersion = new System.Version(requiredVersion),
                        TestedMBVersion = new System.Version(testedVersion),
                        Name = pluginRoot.SafeGetString("Name"),
                        BaseUrl = GetPath(source),
                        InstallGlobally = XmlConvert.ToBoolean(installGlobally.ToLower()),
                        PluginClass = pluginRoot.SafeGetString("PluginClass") ?? assumedClass,
                        UpgradeInfo = pluginRoot.SafeGetString("UpgradeInfo"),
                        IsPremium = XmlConvert.ToBoolean(isPremium.ToLower())
                    });
                }
            } else {

                Logger.ReportWarning("There appears to be no network connection. Plugin can not be installed.");
            }
            return list;
        }

        private string GetPath(string source) {
            var index = source.LastIndexOf("\\");
            if (index<=0) {
                index = source.LastIndexOf("/");
            }
            return source.Substring(0, index);
        }
    }
}
