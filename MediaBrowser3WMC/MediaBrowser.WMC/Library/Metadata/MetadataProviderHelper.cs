using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser.Library.Entities;
using MediaBrowser.Library.Extensions;
using MediaBrowser.Library.Interfaces;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Library.Providers.Attributes;
using System.Collections;
using MediaBrowser.Library.Util;
using System.Diagnostics;
using MediaBrowser.Library.Entities.Attributes;
using System.Threading;
using MediaBrowser.Library.Plugins;
using MediaBrowser.Library.Providers.TVDB;
using MediaBrowser.Library.Providers;
using MediaBrowser.Library.Factories;
using MediaBrowser.Library.Logging;

namespace MediaBrowser.Library.Metadata {
    public class MetadataProviderHelper {


        static object sync = new object();
 
        public static Type[] ProviderTypes { 
            get { 
                return Kernel.Instance.MetadataProviderFactories.Select(p => p.Type).ToArray(); 
            } 
        }

        public static List<MetadataProviderFactory> DefaultProviders() {

            return new Type[] { 
                typeof(VirtualFolderProvider),
                typeof(ImageFromMediaLocationProvider),
                typeof(ImageByNameProvider),
                typeof(VideoFormatProvider),
                typeof(MBMovieProviderFromJson),
                typeof(MovieProviderFromXml),
                typeof(FolderProviderFromXml),
                typeof(LocalEpisodeProvider), 
                typeof(LocalSeriesProvider),
                typeof(LocalSeasonProvider),
                typeof(RemoteEpisodeProvider),
                typeof(RemoteSeasonProvider), 
                typeof(RemoteSeriesProvider),
                typeof(MovieDbProvider),
                typeof(MBDefaultImageProvider)
            }.Select(t => new MetadataProviderFactory(t)).ToList(); 
            
        }


        public static bool UpdateMetadata(BaseItem item, MetadataRefreshOptions options) {

            bool force = (options & MetadataRefreshOptions.Force) == MetadataRefreshOptions.Force;
            bool fastOnly = (options & MetadataRefreshOptions.FastOnly) == MetadataRefreshOptions.FastOnly;

            bool changed = false;
            if (force) {
                ClearItem(item); 
            }

            bool neverSavedProviderInfo;
            var providers = GetSupportedProviders(item, out neverSavedProviderInfo);

            var itemClone = (BaseItem)Serializer.Clone(item);
            // Parent is not serialized so its not cloned
            itemClone.Parent = item.Parent;

            foreach (var provider in providers) {
                provider.Item = itemClone;
            }

            if (force || NeedsRefresh(providers, fastOnly)) {

                // something changed clear the item before pulling metadata 
                if (!force) {
                    ClearItem(item);
                    ClearItem(itemClone);
                }

                // we must clear the provider data as well in case it is bad or out of date! 
                foreach (var provider in providers) {
                    ClearItem(provider);
                }

                Logger.ReportInfo("Metadata changed for the following item {0} (first pass : {1} forced via UI : {2})", item.Name, fastOnly, force);
                changed = UpdateMetadata(item, true, fastOnly, providers);
            }

            if (!changed && neverSavedProviderInfo) {
                Kernel.Instance.ItemRepository.SaveProviders(item.Id, providers);
            }

            return changed;
        }

        /// <summary>
        /// Clear all the persistable parts of the entitiy excluding parts that are updated during initialization
        /// </summary>
        /// <param name="item"></param>
        private static void ClearItem(object item) {
            foreach (var persistable in Serializer.GetPersistables(item)) {
                if (persistable.GetAttributes<NotSourcedFromProviderAttribute>() == null && 
                    persistable.GetAttributes<DontClearOnForcedRefreshAttribute>() == null) {
                    persistable.SetValue(item, null);
                } else
                    if (persistable.GetAttributes<DontClearOnForcedRefreshAttribute>() != null &&
                        persistable.GetValue(item) != null &&
                        persistable is object)
                    {
                        // in case this object itself has persistables that need clearing - only one level, not recursive
                        var innerItem = persistable.GetValue(item);
                        foreach (var innerPersistable in Serializer.GetPersistables(innerItem))
                        {
                            if (innerPersistable.GetAttributes<NotSourcedFromProviderAttribute>() == null &&
                                innerPersistable.GetAttributes<DontClearOnForcedRefreshAttribute>() == null)
                            {
                                innerPersistable.SetValue(innerItem, null);
                            }
                        }
                    }
            }
        }

        static bool NeedsRefresh(IList<IMetadataProvider> supportedProviders, bool fastOnly) {
            foreach (var provider in supportedProviders) {
                try 
                {
                    if ((provider.IsSlow || provider.RequiresInternet) && fastOnly) continue;
                    if (provider.NeedsRefresh())
                    {
                        Logger.ReportVerbose("Provider " + provider.GetType() + " reports need refresh");
                        return true;
                    }
                } catch (Exception e) {
                    Logger.ReportException("Metadata provider failed during NeedsRefresh. Item Path: "+provider.Item.Path, e);
                    Debug.Assert(false, "Providers should catch all the exceptions that NeedsRefresh generates!");
                }
            }
            return false;
        }

        static IList<IMetadataProvider> GetSupportedProviders(BaseItem item, out bool neverSavedProviderInfo) {

       
            var cachedProviders = Kernel.Instance.ItemRepository.RetrieveProviders(item.Id);
            neverSavedProviderInfo = (cachedProviders == null);
            if (cachedProviders == null) {
                cachedProviders = new List<IMetadataProvider>();
            }

            var lookup = cachedProviders.ToDictionary(provider => provider.GetType());

            return Kernel.Instance.MetadataProviderFactories
                .Where(provider => provider.Supports(item))
                .Where(provider => !provider.RequiresInternet || Config.Instance.AllowInternetMetadataProviders)
                .Select(provider => lookup.GetValueOrDefault(provider.Type, provider.Construct()))
                .ToList();
        }


        static bool UpdateMetadata(
            BaseItem item,
            bool force,
            bool fastOnly,
            IEnumerable<IMetadataProvider> providers
            ) 
        {
            bool changed = false;

            foreach (var provider in providers) {

                if ((provider.IsSlow || provider.RequiresInternet) && fastOnly) continue;

                try {
                    if (force || provider.NeedsRefresh()) {
                        //I HATE this but I guess we're already tied to MIP by having a specific property for it...
                        if (provider.GetType().FullName.ToLower() == "mediainfoprovider.mediainfoprovider" && item is Show && (item as Show).MediaInfo != null)
                        {
                            (item as Show).MediaInfo.PluginData = null; //clear this out only if it actually needs a refresh
                        }
                        provider.Fetch();
                        Serializer.Merge(provider.Item, item);
                        changed = true;
                    }
                } catch (Exception e) {
                    Debug.Assert(false, "Meta data provider should not be leaking exceptions");
                    Logger.ReportException("Provider: " + provider.GetType().ToString()+" failed for item at: "+item.Path, e);
                }
            }
            if (changed) {
                Kernel.Instance.ItemRepository.SaveItem(item);
                Kernel.Instance.ItemRepository.SaveProviders(item.Id, providers);
            }

            return changed;
        }
    }
}
