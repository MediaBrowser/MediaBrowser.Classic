using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Configurator
{
    public static class Constants
    {
        public const float MAX_ASPECT_RATIO_STRETCH = 10000;
        public const float MAX_ASPECT_RATIO_DEFAULT = 0.05F;

        public static readonly String ENTRYPOINTS_REGISTRY_PATH = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Media Center\Extensibility\Entry Points";
        public static readonly String CATEGORIES_REGISTRY_PATH = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Media Center\Extensibility\Categories";
        public static readonly String HIDDEN_CATEGORIES_GUID = @"MediaBrowserHidden";
        public static readonly String APPLICATION_ID = @"{ce32c570-4bec-4aeb-ad1d-cf47b91de0b2}";
        public static readonly String MB_MAIN_ENTRYPOINT_GUID = @"{fc9abccc-36cb-47ac-8bab-03e8ef5f6f22}";
        public static readonly String MB_CONFIG_ENTRYPOINT_GUID = @"{b8f02923-484e-483e-b227-e5a810c77724}";

        public static readonly String HKEY_LOCAL_MACHINE = @"HKEY_LOCAL_MACHINE";
    }
}
