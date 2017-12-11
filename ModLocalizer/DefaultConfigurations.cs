using System;

namespace ModLocalizer
{
    internal static class DefaultConfigurations
    {
        public const string DefaultLanguage = "Chinese";

        public const string OutputFileNameFormat = "{0}_patched.tmod";

        public static readonly Version TerrariaVersion = new Version(1, 3, 5, 3);

        public static readonly Version ModLoaderVersion = new Version(0, 10, 1, 1);
    }
}