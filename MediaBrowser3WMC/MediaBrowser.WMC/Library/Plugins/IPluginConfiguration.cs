using System;
namespace MediaBrowser.Library.Plugins {
    public interface IPluginConfiguration {
        bool? BuildUI();
        void Load();
        void Save();
    }
}
