using System;
using System.Collections.Generic;
using System.IO;
using Mod.Localizer.ContentProcessor;

namespace Mod.Localizer
{
    public static class DefaultConfigurations
    {
        public const GameCultures DefaultLanguage = GameCultures.English;

        public const string OutputFileNameFormat = "{0}_patched.tmod";

        public static readonly Version TerrariaVersion = new Version(1, 3, 5, 3);

        public static readonly Version ModLoaderVersion = new Version(0, 10, 1, 1);

        public static readonly IReadOnlyDictionary<Type, string> FolderMapper = new Dictionary<Type, string>
        {
            [typeof(ItemProcessor)] = "Items",
            [typeof(NpcProcessor)] = "NPCs",
            [typeof(BuffProcessor)] = "Buffs",
            [typeof(MiscTextProcessor)] = "Miscs",
            [typeof(TileProcessor)] = "Tiles",
            [typeof(TranslationProcessor)] = "Customs"
        };

        public static string GetPath(TmodFileWrapper.ITmodFile mod, Type processorType, string file)
        {
            return Path.Combine(mod.Name, FolderMapper[processorType], file);
        }
    }
}
