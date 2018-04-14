using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using dnlib.DotNet;
using Mod.Localizer.ContentFramework;
using Mod.Localizer.ContentProcessor;
using Mod.Localizer.Resources;
using Newtonsoft.Json;

namespace Mod.Localizer
{
    internal sealed class PatchEngine : ProcessEngine
    {
        public PatchEngine(string modPath, string sourcePath, GameCultures language) : base(modPath, sourcePath, language)
        {
        }

        protected override void Process()
        {
            const string mono = "Mono.dll";
            if (Mod.HasFile(mono))
            {
                InnerPatch(mono);
            }

            // save mod file
            var file = string.Format(DefaultConfigurations.OutputFileNameFormat, Mod.Name);
            Logger.Warn(Strings.Saving, file);

            Mod.Write(file);
        }

        private void InnerPatch(string dll = null)
        {
            // use default (win/all) platform unless mentioned
            if (string.IsNullOrWhiteSpace(dll))
            {
                dll = Mod.HasFile("All.dll") ? "All.dll" : "Windows.dll";
            }

            if (!Mod.HasFile(dll))
            {
                Logger.Error("Cannot find the assembly file: {0}", dll);
            }

            Logger.Info(Strings.Patching, dll);

            var module = AssemblyDef.Load(Mod.GetFile(dll)).Modules.Single();

            foreach (var processor in Processors)
            {
                try
                {
                    var proc = Activator.CreateInstance(processor, Mod, module, Language);
                    var tran = LoadFilesWrapper(SourcePath, processor);

                    processor.GetMethod(nameof(Processor<Content>.PatchContents))?.Invoke(proc, new[] { tran });

                }
                catch (Exception ex)
                {
                    Logger.Warn(Strings.ProcExceptionOccur, processor.FullName);
                    Logger.Error(ex);
                }
            }

            using (var ms = new MemoryStream())
            {
                module.Assembly.Write(ms);

                Mod.Files[dll] = ms.ToArray();
            }
        }

        private object LoadFilesWrapper(string contentPath, Type processorType)
        {
            Debug.Assert(processorType.BaseType != null, "processorType.BaseType != null");
            var contentType = processorType.BaseType.GetGenericArguments().Single();

            var loadFilesMethod = typeof(Program).GetMethod(nameof(LoadFiles), BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(string) }, null);
            loadFilesMethod = loadFilesMethod?.MakeGenericMethod(processorType, contentType);

            Debug.Assert(loadFilesMethod != null, nameof(loadFilesMethod) + " != null");
            return loadFilesMethod.Invoke(null, new object[] { contentPath });
        }

        private IList<TContent> LoadFiles<TProcessor, TContent>(string contentPath)
            where TContent : Content
            where TProcessor : Processor<TContent>
        {
            if (!DefaultConfigurations.FolderMapper.ContainsKey(typeof(TProcessor)))
            {
                return null;
            }

            var path = Path.Combine(contentPath, DefaultConfigurations.FolderMapper[typeof(TProcessor)]);

            var list = new List<TContent>();

            foreach (var file in Directory.EnumerateFiles(path, "*.json"))
            {
                using (var sr = new StreamReader(File.OpenRead(file)))
                {
                    list.AddRange(JsonConvert.DeserializeObject<List<TContent>>(sr.ReadToEnd()));
                }
            }

            Logger.Debug("Loaded from {0}", path);

            return list;
        }
    }
}
