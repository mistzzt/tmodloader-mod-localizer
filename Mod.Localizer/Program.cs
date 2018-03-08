using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.CommandLineUtils;
using NLog;
using NLog.Config;
using NLog.Targets;
using System.Globalization;
using System.IO;
using System.Linq;
using dnlib.DotNet;
using Mod.Localizer.ContentFramework;
using Mod.Localizer.ContentProcessor;
using Mod.Localizer.Resources;
using Newtonsoft.Json;

namespace Mod.Localizer
{
    internal static class Program
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private static bool _dump = true;
        private static string _modFilePath, _sourcePath;
        private static GameCultures _language = DefaultConfigurations.DefaultLanguage;

        private static void Process()
        {
            var wrapper = new TmodFileWrapper(typeof(Terraria.BitsByte).Assembly);
            var modFile = wrapper.LoadFile(_modFilePath);

            var processors =
                typeof(Program)
                    .Assembly
                    .GetTypes()
                    .Where(t => t.BaseType?.IsGenericType == true &&
                                t.BaseType.GetGenericTypeDefinition() == typeof(Processor<>));

            Directory.CreateDirectory(modFile.Name);
            foreach (var folder in DefaultConfigurations.FolderMapper.Values)
            {
                Directory.CreateDirectory(modFile.Name + Path.DirectorySeparatorChar + folder);
            }

            if (_dump)
            {
                Dump(modFile, processors);
            }
            else
            {
                Patch(modFile, processors);
            }
        }

        private static void Dump(TmodFileWrapper.ITmodFile modFile, IEnumerable<Type> processors)
        {
            var module = AssemblyDef.Load(modFile.GetMainAssembly()).Modules.Single();

            foreach (var processor in processors)
            {
                try
                {
                    var proc = Activator.CreateInstance(processor, modFile, module, _language);

                    var contents =
                        (IReadOnlyList<Content>)processor.GetMethod(nameof(Processor<Content>.DumpContents))?.Invoke(proc, new object[0]);

                    if (contents == null)
                    {
                        Logger.Warn("Processor {0} not used!", processor.Name);
                        continue;
                    }

                    Logger.Debug("Using " + processor.Name);

                    foreach (var val in contents.GroupBy(x => x.Namespace, x => x))
                    {
                        File.WriteAllText(
                            DefaultConfigurations.GetPath(modFile, processor, val.Key + ".json"),
                            JsonConvert.SerializeObject(val.ToList(), Formatting.Indented)
                        );

                        Logger.Info("Dumping namespace {0}", val.Key);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn("Unhandled exception");
                    Logger.Error(ex);
                }
            }
        }

        private static void Patch(TmodFileWrapper.ITmodFile modFile, IEnumerable<Type> processors)
        {
            const string mono = "Mono.dll";

            var procs = processors as Type[] ?? processors.ToArray();
            if (modFile.HasFile(mono))
            {
                InnerPatch(modFile, procs, mono);
            }

            InnerPatch(modFile, procs);
        }

        private static void InnerPatch(TmodFileWrapper.ITmodFile modFile, IEnumerable<Type> processors, string dll = null)
        {
            if (string.IsNullOrWhiteSpace(dll))
            {
                dll = modFile.HasFile("All.dll") ? "All.dll" : "Windows.dll";
            }

            var module = AssemblyDef.Load(modFile.GetFile(dll)).Modules.Single();
            
            foreach (var processor in processors)
            {
                try
                {
                    var proc = Activator.CreateInstance(processor, modFile, module, _language);
                    var tran = LoadFiles(_sourcePath, processor);

                    processor.GetMethod(nameof(Processor<Content>.PatchContents))?.Invoke(proc, new [] {tran});

                }
                catch (Exception ex)
                {
                    Logger.Warn("Unhandled exception");
                    Logger.Error(ex);
                }
            }

            using (var ms = new MemoryStream())
            {
                module.Assembly.Write(ms);

                modFile.Files[dll] = ms.ToArray();
            }
        }

        private static object LoadFiles(string folder, Type procType)
        {
            if (!DefaultConfigurations.FolderMapper.ContainsKey(procType))
            {
                return null;
            }

            Debug.Assert(procType.BaseType != null, "procType.BaseType != null");
            var contentType = procType.BaseType.GetGenericArguments().Single();
            var listType = typeof(List<>).MakeGenericType(contentType);

            var doubleListType = typeof(List<>).MakeGenericType(listType);

            var translations = Activator.CreateInstance(doubleListType);
            var addMethod = doubleListType.GetMethod(nameof(List<object>.Add));
            
            foreach (var file in Directory.EnumerateFiles(Path.Combine(folder, DefaultConfigurations.FolderMapper[procType]), "*.json"))
            {
                using (var sr = new StreamReader(File.OpenRead(file)))
                {
                    var list = JsonConvert.DeserializeObject(sr.ReadToEnd(), listType);
                    addMethod.Invoke(translations, new [] {list});
                }
            }

            return typeof(Program).GetMethod(nameof(CombineLists)).MakeGenericMethod(contentType).Invoke(null, new object[] {translations});
        }

        public static List<T> CombineLists<T>(List<List<T>> list)
        {
            return list.SelectMany(x => x).ToList();
        }

        public static void Main(string[] args)
        {
            ConfigureLogger();
#if DEBUG
            System.Threading.Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
#endif

            if (args.Length == 0)
            {
                args = new[]
                {
#if DEBUG
                    "--mode", "patch",
                    "-f", "ThoriumMod",
                    "Test.tmod",
#else
                    "-h"
#endif
                };
            }

            Logger.Debug($"{typeof(Program).Namespace} started. (v{typeof(Program).Assembly.GetName().Version})");

            if (ParseCliArguments(args))
            {
                Process();

                Logger.Info(Strings.ProcessComplete);
            }

#if DEBUG
            Console.ReadLine();
#endif
        }

        private static bool ParseCliArguments(string[] args)
        {
            var appVersion = typeof(Program).Assembly.GetName().Version;

            var app = new CommandLineApplication(false)
            {
                Name = typeof(Program).Namespace,
                FullName = typeof(Program).Namespace,
                ShortVersionGetter = () => appVersion.ToString(2),
                LongVersionGetter = () => appVersion.ToString(3)
            };

            app.HelpOption("--help | -h");
            app.VersionOption("-v | --version", appVersion.ToString(2), appVersion.ToString(3));

            var modeOpt = app.Option("-m | --mode", Strings.ProgramModeDesc, CommandOptionType.SingleValue);
            var srcOpt = app.Option("-f | --folder", Strings.SourceFolderDesc, CommandOptionType.SingleValue);
            var langOpt = app.Option("-l | --language", Strings.LanguageDesc, CommandOptionType.SingleValue);

            var modFilePathArg = app.Argument(Strings.PathArgumentName, Strings.PathDesc);

            app.OnExecute(() =>
            {
                if (modeOpt.HasValue())
                {
                    _dump = !string.Equals(modeOpt.Value(), "patch", StringComparison.OrdinalIgnoreCase);
                }

                if (srcOpt.HasValue())
                {
                    _sourcePath = srcOpt.Value();
                }

                if (langOpt.HasValue())
                {
                    if (!Enum.TryParse(langOpt.Value(), out _language))
                    {
                        Logger.Error("Invalid game culture!");
                    }
                }

                _modFilePath = modFilePathArg.Value;

                return 0;
            });

            app.Execute(args);

            // validate arguments
            if (string.IsNullOrWhiteSpace(_modFilePath))
            {
                Logger.Error(Strings.NoFileSpecified);
                return false;
            }

            // ReSharper disable once InvertIf
            if (string.IsNullOrWhiteSpace(_sourcePath) && !_dump)
            {
                Logger.Error(Strings.NoSourceFolderSpecified);
                return false;
            }

            return true;
        }

        private static void ConfigureLogger()
        {
            const string layout = "${date:format=HH\\:mm\\:ss}\t|${level}\t|${logger}\t|${message}\t";

            var config = new LoggingConfiguration();

            var consoleTarget = new ColoredConsoleTarget
            {
                Layout = layout
            };
            var fileTarget = new FileTarget
            {
                FileName = $"localizer.{DateTime.Now:MM_dd.hh_mm}.log",
                Layout = layout
            };

            config.AddTarget("console", consoleTarget);
            config.AddTarget("file", fileTarget);

            config.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, consoleTarget));
            config.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, fileTarget));

            LogManager.Configuration = config;
        }
    }
}
