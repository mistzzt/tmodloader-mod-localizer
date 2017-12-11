using System;
using Newtonsoft.Json;

namespace ModLocalizer.ModLoader
{
    [JsonObject(MemberSerialization.Fields)]
    internal sealed class TmodProperties
    {
        public string Name;

        public string ModVersion = new Version(1, 0).ToString();

        public string ModLoaderVersion = DefaultConfigurations.ModLoaderVersion.ToString();
    }
}
