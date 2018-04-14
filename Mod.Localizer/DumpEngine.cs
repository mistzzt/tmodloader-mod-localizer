using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using dnlib.DotNet;
using Mod.Localizer.ContentFramework;
using Mod.Localizer.ContentProcessor;
using Mod.Localizer.Resources;
using Newtonsoft.Json;

namespace Mod.Localizer
{
    internal sealed class DumpEngine : ProcessEngine
    {
        private readonly ModuleDef _module;

        public DumpEngine(string modPath, string sourcePath, GameCultures language) : base(modPath, sourcePath, language)
        {
           _module = AssemblyDef.Load(Mod.GetMainAssembly()).Modules.Single();
        }

        protected override void Process()
        {
            foreach (var processor in Processors)
            {
                try
                {
                    UseProcessor(processor);
                }
                catch (Exception ex)
                {
                    Logger.Warn(Strings.ProcExceptionOccur, processor.FullName);
                    Logger.Error(ex);
                }
            }
        }

        private void UseProcessor(Type processor)
        {
            var proc = Activator.CreateInstance(processor, Mod, _module, Language);

            var contents =
                (IReadOnlyList<Content>)processor.GetMethod(nameof(Processor<Content>.DumpContents))?.Invoke(proc, new object[0]);

            if (contents == null)
            {
                Logger.Warn(Strings.ProcNotUsed, processor.Name);
                return;
            }

            Logger.Debug("Using " + processor.Name);

            foreach (var val in contents.GroupBy(x => x.Namespace, x => x))
            {
                File.WriteAllText(
                    DefaultConfigurations.GetPath(Mod, processor, val.Key + ".json"),
                    JsonConvert.SerializeObject(val.ToList(), Formatting.Indented)
                );

                Logger.Info(Strings.DumpNamespace, val.Key);
            }
        }
    }
}
