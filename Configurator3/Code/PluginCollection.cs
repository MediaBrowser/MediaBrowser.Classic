using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Plugins;
using MediaBrowser.Library;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using MediaBrowser.Library.Logging;

namespace Configurator.Code {

    // sample data 
    public class SamplePlugin : IPlugin {

        public MBLoadContext InitDirective { get; set; }

        public string PluginClass { get { return PluginClasses.Themes; } }

        public void Init(Kernel kernel) {
        }

        public string Name {
            get; set;
        }

        public string Description {
            get; set;
        }

        public string RichDescURL
        {
            get;
            set;
        }

        public System.Version Version {
            get { return new System.Version(1, 2, 3, 4);  }
        }

        public System.Version RequiredMBVersion
        {
            get { return new System.Version(1, 2, 3, 4); }
        }

        public System.Version TestedMBVersion
        {
            get { return new System.Version(1, 2, 3, 4); }
        }

        public string Filename {
            get { return "bob.dll"; }
        }

        public virtual bool IsConfigurable
        {
            get
            {
                return false;
            }
        }

        public virtual void Configure()
        {
        }

        public virtual bool InstallGlobally
        {
            get { return false; }
        }

        public virtual bool Installed
        {
            get { return true; }
            set { }
        }

        public virtual bool IsLatestVersion
        {
            get { return true; }
            set { }
        }

        public virtual bool UpdateAvail
        {
            get;
            set;
        }

        public virtual bool IsPremium
        {
            get;
            set;
        }

        public string ListDisplayString
        {
            get { return Name + " (v" + Version + ")"; }
        }

        public virtual string UpgradeInfo
        {
            get { return ""; }
        }

        public void UnInstalling()
        { }
    }

    public class PluginCollection : ObservableCollection<IPlugin> {

        public PluginCollection() {

            //if (DesignerProperties.GetIsInDesignMode(new DependencyObject())) {

            //    Add(new SamplePlugin() { Name = "Super Plugin", 
            //        Description = "This plugin does absoulutly nothing, its actually a sample plugin for wpf to bind to.", UpdateAvail = false});
            //    Add(new SamplePlugin() { Name = "The other plugin", 
            //        Description = "This plugin also does absoulutly nothing, its actually a sample plugin for wpf to bind to.", UpdateAvail = true });

            //} 
        }

        public IPlugin Find(IPlugin plugin)
        {
            return this.Items.ToList().Find(p => p.Filename.ToLower() == plugin.Filename.ToLower());
        }

        public IPlugin Find(IPlugin plugin, System.Version version)
        {
            return this.Items.ToList().Find(p => p.Filename.ToLower() == plugin.Filename.ToLower() && p.Version == version);
        }

        public IPlugin Find(IPlugin plugin, bool installed)
        {
            return this.Items.ToList().Find(p => p.Filename == plugin.Filename && p.Installed == installed);
        }
    }
}
