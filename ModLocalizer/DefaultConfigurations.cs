using System;

namespace ModLocalizer
{
    internal static class DefaultConfigurations
    {
        public const string DefaultLanguage = "Chinese";

        public const string OutputFileNameFormat = "{0}_patched.tmod";

        public static readonly Version TerrariaVersion = new Version(1, 3, 5, 3);

        public static readonly Version ModLoaderVersion = new Version(0, 10, 1, 1);

        public static class LocalizerFiles
        {
            public const string ItemFolder = "Items";

            public const string NpcFolder = "NPCs";

            public const string BuffFolder = "Buffs";

            public const string MiscFolder = "Miscs";

            public const string TileFolder = "Tiles";

            public const string CustomFolder = "Customs";

            public const string InfoConfigurationFile = "Info.json";

            public const string ModInfoConfigurationFile = "ModInfo.json";
        }
    }
}