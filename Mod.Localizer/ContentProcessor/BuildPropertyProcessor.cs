using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using dnlib.DotNet;
using Mod.Localizer.ContentFramework;
using Mod.Localizer.Extensions;
using Mod.Localizer.Resources;
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
            var path = this.GetExtraDataPath();
            var json = _helper.Load(ModFile);

            File.WriteAllText(path, json);
            Logger.Info(Strings.BuildPropWrittenToPath, path);

            return new List<Content>();
        }

        public override void PatchContents(IReadOnlyList<Content> contents)
        {
            var path = this.GetExtraDataPath();
            if (!File.Exists(path))
            {
                return;
            }

            using (var sr = new StreamReader(File.OpenRead(path)))
            {
                var json = sr.ReadToEnd();
                
                _helper.Write(ModFile, json);
            }
        }

        private sealed class BuildPropHelper
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
                // Creates a new blank information object from json data
                var obj = JsonConvert.DeserializeObject(json, _readBuildFile.DeclaringType, new JsonSerializerSettings
                {
                    ContractResolver = new StringFieldContractResolver()
                });

                // Loads the property object within current mod
                var info = LoadRaw(modFile);

                // Replaces target fields
                foreach (var field in obj.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).Where(FieldSelector))
                {
                    var replace = field.GetValue(obj);
                    field.SetValue(info, replace);
                }

                // Serializes the property object and store it inside mod file
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

            /// <summary>
            /// Resolver to include all private fields in <see cref="Terraria.ModLoader.BuildProperties"/>.
            /// </summary>
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

            /// <summary>
            /// Selects all fields of <see cref="Terraria.ModLoader.BuildProperties"/> that should be included in serialization.
            /// </summary>
            private static bool FieldSelector(FieldInfo f) => f.FieldType == typeof(string);
        }
    }
}
