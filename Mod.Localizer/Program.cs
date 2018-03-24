using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using dnlib.DotNet;
using Microsoft.Extensions.CommandLineUtils;
using Mod.Localizer.ContentFramework;
using Mod.Localizer.ContentProcessor;
using Mod.Localizer.Resources;
using Newtonsoft.Json;
using NLog;
using NLog.Config;
using NLog.Targets;
using Terraria;

namespace Mod.Localizer
{
    internal static class Program
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private static bool _dump = true;
        private static string _modFilePath;
        private static GameCultures _language = DefaultConfigurations.DefaultLanguage;

        /// <summary>
        /// Source folder path for processors accessing extra files.
        /// </summary>
        internal static string SourcePath { get; private set; }

        private static void Process()
        {
            var wrapper = new TmodFileWrapper(typeof(BitsByte).Assembly);
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

            Logger.Warn("Directory created: {0}", modFile.Name);

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
                        Logger.Warn(Strings.ProcNotUsed, processor.Name);
                        continue;
                    }

                    Logger.Debug("Using " + processor.Name);

                    foreach (var val in contents.GroupBy(x => x.Namespace, x => x))
                    {
                        File.WriteAllText(
                            DefaultConfigurations.GetPath(modFile, processor, val.Key + ".json"),
                            JsonConvert.SerializeObject(val.ToList(), Formatting.Indented)
                        );

                        Logger.Info(Strings.DumpNamespace, val.Key);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(Strings.ProcExceptionOccur, processor.FullName);
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

            // save mod file
            var file = string.Format(DefaultConfigurations.OutputFileNameFormat, modFile.Name);
            Logger.Warn(Strings.Saving, file);

            modFile.Write(file);
        }

        private static void InnerPatch(TmodFileWrapper.ITmodFile modFile, IEnumerable<Type> processors, string dll = null)
        {
            if (string.IsNullOrWhiteSpace(dll))
            {
                dll = modFile.HasFile("All.dll") ? "All.dll" : "Windows.dll";
            }

            Logger.Info(Strings.Patching, dll);

            var module = AssemblyDef.Load(modFile.GetFile(dll)).Modules.Single();

            foreach (var processor in processors)
            {
                try
                {
                    var proc = Activator.CreateInstance(processor, modFile, module, _language);
                    var tran = LoadFiles(SourcePath, processor);

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

                modFile.Files[dll] = ms.ToArray();
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

        private static IList<TContent> LoadFiles<TProcessor, TContent>(string contentPath)
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

        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, eventArgs) =>
            {
                var resourceName = new AssemblyName(eventArgs.Name).Name + ".dll";
                var programDirectory = Assembly.GetExecutingAssembly().Location;

                if (File.Exists(Path.Combine(programDirectory, resourceName)))
                {
                    return Assembly.Load(File.ReadAllBytes(Path.Combine(programDirectory, resourceName)));
                }

                var text = Array.Find(typeof(BitsByte).Assembly.GetManifestResourceNames(), element => element.EndsWith(resourceName));
                if (text == null)
                {
                    return null;
                }

                using (var manifestResourceStream = typeof(BitsByte).Assembly.GetManifestResourceStream(text))
                {
                    if (manifestResourceStream == null)
                    {
                        return null;
                    }

                    var data = new byte[manifestResourceStream.Length];
                    manifestResourceStream.Read(data, 0, data.Length);
                    return Assembly.Load(data);
                }
            };

            AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
            {
                var sb = new StringBuilder("Unhandled Exception")
                    .AppendLine()
                    .Append("================\r\n")
                    .AppendFormat("{0}: Unhandled Exception\r\nCulture: {1}\r\nException: {2}\r\n",
                        DateTime.Now,
                        Thread.CurrentThread.CurrentCulture.Name,
                        eventArgs.ExceptionObject.ToString())
                    .Append("================\r\n");

                Logger.Error(sb);

                Environment.Exit(1);
            };


            ConfigureLogger();
#if DEBUG
            //Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
#endif

            if (args.Length == 0)
            {
                args = new[]
                {
#if DEBUG
                    "--mode", "patch",
                    "-f", "ThoriumMod",
                    "-l", "English",
                    "Test.tmod",
#else
                    "-h"
#endif
                };
            }

            Logger.Debug(Strings.ProgramVersion, typeof(Program).Namespace, typeof(Program).Assembly.GetName().Version);

            if (ParseCliArguments(args))
            {
                Process();

                Logger.Fatal(Strings.ProcessComplete);
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
                    SourcePath = srcOpt.Value();
                }

                if (langOpt.HasValue())
                {
                    if (!Enum.TryParse(langOpt.Value(), out _language))
                    {
                        Logger.Error(Strings.InvalidGameCulture);
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
            if (string.IsNullOrWhiteSpace(SourcePath) && !_dump)
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
                Layout = layout,
                Encoding = Encoding.UTF8
            };
            var fileTarget = new FileTarget
            {
                FileName = $"localizer.{DateTime.Now:MM_dd.hh_mm}.log",
                Layout = layout,
                Encoding = Encoding.UTF8
            };

            config.AddTarget("console", consoleTarget);
            config.AddTarget("file", fileTarget);

            config.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, consoleTarget));
            config.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, fileTarget));

            LogManager.Configuration = config;
        }
    }
}
