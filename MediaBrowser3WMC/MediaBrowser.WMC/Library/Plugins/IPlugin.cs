using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Factories;

namespace MediaBrowser.Library.Plugins {
    /// <summary>
    /// This interface can be implemented by plugin to provide rich information about the plugin
    ///  It also provides plugins with a place to place initialization code
    /// </summary>
    public interface IPlugin {
        void Init(Kernel kernel);
        string Filename { get; }
        string Name { get; }
        string Description { get; }
        string RichDescURL { get; }
        System.Version Version { get; }
        System.Version RequiredMBVersion { get; }
        System.Version TestedMBVersion { get; }
        bool IsConfigurable { get; }
        void Configure();
        bool InstallGlobally { get; }
        /// <summary>
        /// Context in which this plugin should be intialized
        /// </summary>
        MBLoadContext InitDirective { get; }
        string UpgradeInfo { get; }
        string PluginClass { get; }
        bool UpdateAvail { get; set; }
        bool Installed { get; set; }
        bool IsLatestVersion { get; set; }
        string ListDisplayString { get; }
        bool IsPremium { get; }
        void UnInstalling();
    }

    public static class PluginClasses
    {
        public const string Themes = "Themes";
        public const string ScreenSavers = "ScreenSavers";
        public const string Other = "Other";
    }
}
