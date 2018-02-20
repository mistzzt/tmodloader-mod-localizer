using System;
using Microsoft.Extensions.CommandLineUtils;
using NLog;
using NLog.Config;
using NLog.Targets;
using System.Globalization;
using System.Linq;
using System.Reflection;
using dnlib.DotNet;
using Mod.Localizer.ContentProcessor;
using Mod.Localizer.Resources;

namespace Mod.Localizer
{
    internal static class Program
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

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

            var app = new CommandLineApplication(false)
            {
                FullName = typeof(Program).Namespace,
                ShortVersionGetter = () => typeof(Program).Assembly.GetName().Version.ToString(2),
                LongVersionGetter = () => typeof(Program).Assembly.GetName().Version.ToString(3)
            };

            var version = typeof(Program).Assembly.GetName().Version;
            app.HelpOption("--help | -h");
            app.VersionOption("-v | --version", version.ToString(2), version.ToString(3));

            var pathArgument = app.Argument(Strings.PathArgumentName, Strings.PathDesc);
            var modeOption = app.Option("-m | --mode", Strings.ProgramModeDesc, CommandOptionType.SingleValue);
            var folderOption = app.Option("-f | --folder", Strings.SourceFolderDesc, CommandOptionType.SingleValue);
            var languageOption = app.Option("-l | --language", Strings.LanguageDesc, CommandOptionType.SingleValue);

            var dump = true;
            string folder = null, path, language = "Chinese";

            app.OnExecute(() =>
            {
                if (modeOption.HasValue())
                {
                    dump = !string.Equals(modeOption.Value(), "patch", StringComparison.OrdinalIgnoreCase);
                }

                if (folderOption.HasValue())
                {
                    folder = folderOption.Value();
                }

                if (languageOption.HasValue())
                {
                    language = languageOption.Value();
                }

                path = pathArgument.Value;
                if (string.IsNullOrWhiteSpace(path))
                {
                    Logger.Error(Strings.NoFileSpecified);
                    Environment.Exit(1);
                }

                if (!dump && string.IsNullOrWhiteSpace(folder))
                {
                    Logger.Error(Strings.NoSourceFolderSpecified);
                    Environment.Exit(1);
                }

                return 0;
            });

            app.Execute(args);

            Test();

#if DEBUG
            Console.ReadLine();
#endif
        }

        private static void Test()
        {
            var wrapper = new TmodFileWrapper(typeof(Terraria.BitsByte).Assembly);
            var mod = wrapper.LoadFile("Test.tmod");

            var assembly = AssemblyDef.Load(mod.GetMainAssembly());
            var module = assembly.Modules.Single();

            var contents = new ItemProcessor(mod, module).DumpContents();
            foreach (var content in contents)
            {
                Console.WriteLine(content.Name);
                Console.WriteLine(content.ToolTip);
            }
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
