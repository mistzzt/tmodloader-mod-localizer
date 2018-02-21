using System;
using System.Diagnostics;
using Microsoft.Extensions.CommandLineUtils;
using NLog;
using NLog.Config;
using NLog.Targets;
using System.Globalization;
using System.Linq;
using dnlib.DotNet;
using Mod.Localizer.ContentProcessor;
using Mod.Localizer.Resources;

namespace Mod.Localizer
{
    internal static class Program
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private static bool _dump = true;
        private static string _modFilePath, _sourcePath;
        private static string _language = "Chinese";

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
                    "Test.tmod"
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

        private static void Process()
        {
            Test();
        }

        [Conditional("DEBUG")]
        private static void Test()
        {
            var wrapper = new TmodFileWrapper(typeof(Terraria.BitsByte).Assembly);
            var mod = wrapper.LoadFile("Test.tmod");

            var assembly = AssemblyDef.Load(mod.GetMainAssembly());
            var module = assembly.Modules.Single();

            var processor = new ItemProcessor(mod, module);

            var contents = processor.DumpContents();

            foreach (var content in contents.Where(t=>t.ModifyTooltips.Count > 0))
            {
                Logger.Debug(content.ToString);
            }

#if FALSE
            var processors =
 typeof(Program).Assembly.GetTypes().Where(t => t.BaseType?.IsGenericType == true && t.BaseType.GetGenericTypeDefinition() == typeof(Processor<>));
            foreach (var processor in processors)
            {
                try
                {
                    var proc = Activator.CreateInstance(processor, mod, module);

                    var contents =
 (IReadOnlyList<Content>)processor.GetMethod(nameof(Processor<Content>.DumpContents)).Invoke(proc, new object[0]);

                    Logger.Debug("Using " + processor.Name);

                    foreach (var content in contents)
                    {
                        Logger.Debug(content.ToString);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn("Unhandled exception");
                    Logger.Error(ex);
                }
            }
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
                    _language = langOpt.Value();
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
