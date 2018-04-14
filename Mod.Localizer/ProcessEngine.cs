using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Mod.Localizer.ContentProcessor;
using NLog;
using Terraria;

namespace Mod.Localizer
{
    public abstract class ProcessEngine
    {
        public string ModPath { get; }

        public string SourcePath { get; }

        public GameCultures Language { get; }

        protected readonly TmodFileWrapper.ITmodFile Mod;

        protected readonly IReadOnlyList<Type> Processors;

        protected readonly Logger Logger = LogManager.GetCurrentClassLogger();

        protected ProcessEngine(string modPath, string sourcePath, GameCultures language)
        {
            ModPath = modPath;
            SourcePath = sourcePath;
            Language = language;

            var wrapper = new TmodFileWrapper(typeof(BitsByte).Assembly);
            Mod = wrapper.LoadFile(ModPath);

            Processors = new List<Type>();
            SetupProcessors((IList<Type>)Processors);
        }

        public void Run()
        {
            Logger.Warn("Checking directories...");
            SetupDirectories();

            Logger.Warn("Begin process...");
            Process();
        }

        protected abstract void Process();

        protected virtual void SetupDirectories()
        {
            Directory.CreateDirectory(Mod.Name);
            foreach (var folder in DefaultConfigurations.FolderMapper.Values)
            {
                Directory.CreateDirectory(Mod.Name + Path.DirectorySeparatorChar + folder);
            }

            Logger.Warn("Directory created: {0}", Mod.Name);
        }

        protected static void SetupProcessors([Out] IList<Type> list)
        {
            var processors =
                typeof(ProcessEngine)
                    .Assembly
                    .GetTypes()
                    .Where(t => t.BaseType?.IsGenericType == true &&
                                t.BaseType.GetGenericTypeDefinition() == typeof(Processor<>));

            foreach (var processor in processors)
            {
                list.Add(processor);
            }
        }
    }
}
