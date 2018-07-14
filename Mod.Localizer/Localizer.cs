using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using dnlib.DotNet;
using Mod.Localizer.ContentFramework;
using Mod.Localizer.Processor;
using Mod.Localizer.Resources;
using Newtonsoft.Json;
using NLog;

namespace Mod.Localizer
{
    public sealed class Localizer
    {
        public string LocalizationSourcePath { get; private set; }
        public RunningMode Mode { get; }
        public GameCultures Language { get; }
        public string ModPath { get; }
        
        public TmodFileWrapper.ITmodFile Mod { get; private set; }
        
        private readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public Localizer(string modPath, string srcPath = null, RunningMode mode = RunningMode.Dump,
            GameCultures lang = GameCultures.English)
        {
            if (string.IsNullOrEmpty(modPath) || !File.Exists(modPath))
            {
                throw new ArgumentException("Invalid mod path given.", nameof(modPath));
            }

            if (mode == RunningMode.Patch && !Directory.Exists(srcPath))
            {
                throw new ArgumentException("Invalid source path given.", nameof(srcPath));
            }

            ModPath = modPath;
            LocalizationSourcePath = srcPath;
            Mode = mode;
            Language = lang;
        }

        public void Run()
        {
            try
            {
                var wrapper = new TmodFileWrapper(typeof(Terraria.BitsByte).Assembly);
                Mod = wrapper.LoadFile(ModPath);
            }
            catch (Exception ex)
            {
                throw new Exception("Cannot initialize mod file instance", ex);
            }

            var processors = GetProcessors();

            switch (Mode)
            {
                case RunningMode.Dump:
                    Dump(processors);
                    break;
                case RunningMode.Patch:
                    Patch(processors);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void Patch(IEnumerable<Type> processors)
        {
            const string mono = "Mono.dll";
            const string windows = "Windows.dll";
            const string all = "All.dll";

            // determine which dll file to use
            var main = Mod.HasFile(windows) ? windows : all;

            // patch dll files on all platforms
            var procs = processors.ToArray();
            if (Mod.HasFile(mono)) InnerPatch(procs, mono);
            InnerPatch(procs, main);
            
            // save mod file
            var file = string.Format(DefaultConfigurations.OutputFileNameFormat, Mod.Name);
            _logger.Warn(Strings.Saving, file);
            Mod.Write(file);
        }

        private void InnerPatch(IEnumerable<Type> processors, string dll)
        {
            _logger.Info(Strings.Patching, dll);

            var module = AssemblyDef.Load(Mod.GetFile(dll)).Modules.Single();

            foreach (var processor in processors)
            {
                try
                {
                    var proc = Activator.CreateInstance(processor, this, module);
                    var tran = LoadFiles(LocalizationSourcePath, processor);

                    processor.GetMethod(nameof(Processor<Content>.PatchContents))?.Invoke(proc, new[] { tran });

                }
                catch (Exception ex)
                {
                    _logger.Warn(Strings.ProcExceptionOccur, processor.FullName);
                    _logger.Error(ex);
                }
            }

            using (var ms = new MemoryStream())
            {
                module.Assembly.Write(ms);

                Mod.Files[dll] = ms.ToArray();
            }
        }

        private static object LoadFiles(string contentPath, Type processorType)
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

            _logger.Debug("Loaded from {0}", path);

            return list;
        }
        
        private void Dump(IEnumerable<Type> processors)
        {
            LocalizationSourcePath = LocalizationSourcePath ?? Mod.Name;
            
            if (Directory.Exists(LocalizationSourcePath) &&
                DefaultConfigurations.FolderMapper.Values.Any(dir =>
            {
                var path = Path.Combine(LocalizationSourcePath, dir);

                return Directory.Exists(path) && Directory.GetFiles(path).Length > 0;
            }))
            {
                throw new Exception("Non-empty source folder as output path");
            }
            
            Directory.CreateDirectory(LocalizationSourcePath);
            foreach (var folder in DefaultConfigurations.FolderMapper.Values)
            {
                Directory.CreateDirectory(Path.Combine(LocalizationSourcePath, folder));
            }

            _logger.Warn("Directory created: {0}", Mod.Name);

            var module = AssemblyDef.Load(Mod.GetMainAssembly()).Modules.Single();

            foreach (var processor in processors)
            {
                try
                {
                    var proc = Activator.CreateInstance(processor, this, module);

                    var contents =
                        (IReadOnlyList<Content>)processor.GetMethod(nameof(Processor<Content>.DumpContents))?.Invoke(proc, new object[0]);

                    if (contents == null)
                    {
                        _logger.Warn(Strings.ProcNotUsed, processor.Name);
                        continue;
                    }

                    _logger.Debug("Using " + processor.Name);

                    foreach (var val in contents.GroupBy(x => x.Namespace, x => x))
                    {
                        File.WriteAllText(
                            DefaultConfigurations.GetPath(Mod, processor, val.Key + ".json"),
                            JsonConvert.SerializeObject(val.ToList(), Formatting.Indented)
                        );

                        _logger.Info(Strings.DumpNamespace, val.Key);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn(Strings.ProcExceptionOccur, processor.FullName);
                    _logger.Error(ex);
                }
            }
        }
        
        private static IEnumerable<Type> GetProcessors()
        {
            return typeof(Localizer)
                .Assembly
                .GetTypes()
                .Where(t => t.BaseType?.IsGenericType == true &&
                            t.BaseType.GetGenericTypeDefinition() == typeof(Processor<>));
        }
    }
}