using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using dnlib.DotNet;
using Mod.Localizer.ContentFramework;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Terraria;

namespace Mod.Localizer.ContentProcessor
{
    public sealed class BuildPropertyProcessor : Processor<Content>
    {
        private readonly BuildPropHelper _helper = new BuildPropHelper();

        public BuildPropertyProcessor(TmodFileWrapper.ITmodFile modFile, ModuleDef modModule, GameCultures culture) : base(modFile, modModule, culture)
        {
        }

        public override IReadOnlyList<Content> DumpContents()
        {
            var json = _helper.Load(ModFile);

            _helper.Write(ModFile, json);

            return new List<Content>();
        }

        public override void PatchContents(IReadOnlyList<Content> contents)
        {

        }

        private class BuildPropHelper
        {
            public string Load(TmodFileWrapper.ITmodFile modFile)
            {
                var prop = LoadRaw(modFile);

                var json = JsonConvert.SerializeObject(prop, new JsonSerializerSettings
                {
                    ContractResolver = new StringFieldContractResolver(),
                    Formatting = Formatting.Indented
                });

                return json;
            }

            private object LoadRaw(TmodFileWrapper.ITmodFile modFile)
            {
                return _readBuildFile.Invoke(null, new[] { modFile.Instance });
            }

            public void Write(TmodFileWrapper.ITmodFile modFile, string json)
            {
                var obj = JsonConvert.DeserializeObject(json, _readBuildFile.DeclaringType, new JsonSerializerSettings
                {
                    ContractResolver = new StringFieldContractResolver()
                });

                var info = LoadRaw(modFile);

                foreach (var field in obj.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).Where(FieldSelector))
                {
                    var replace = field.GetValue(obj);
                    field.SetValue(info, replace);
                }

                var data = (byte[])_toBytes.Invoke(info, new object[0]);
                modFile.Files["Info"] = data;
            }

            public BuildPropHelper()
            {
                var type = typeof(BitsByte).Assembly
                    .GetType("Terraria.ModLoader.BuildProperties");
                var modFileType = type.Assembly.GetType("Terraria.ModLoader.IO.TmodFile");

                _readBuildFile = type.GetMethod("ReadModFile", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { modFileType }, null);
                _toBytes = type.GetMethod("ToBytes", BindingFlags.Instance | BindingFlags.NonPublic);
            }

            private readonly MethodInfo _readBuildFile;
            private readonly MethodInfo _toBytes;

            private sealed class StringFieldContractResolver : DefaultContractResolver
            {
                protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
                {
                    var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                        .Where(FieldSelector)
                        .Select(f => CreateProperty(f, memberSerialization))
                        .ToList();
                    fields.ForEach(p => { p.Writable = p.Readable = true; });
                    return fields;
                }
            }

            private static bool FieldSelector(FieldInfo f) => f.FieldType == typeof(string);
        }
    }
}
