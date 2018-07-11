using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.Extensions.CommandLineUtils;
using Mod.Localizer.Resources;
using NLog;
using NLog.Config;
using NLog.Targets;
using Terraria;

namespace Mod.Localizer
{
    internal static class Program
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private static RunningMode _mode;
        private static string _modFilePath;
        private static GameCultures _language = DefaultConfigurations.DefaultLanguage;

        /// <summary>
        /// Source folder path for processors accessing extra files.
        /// </summary>
        internal static string SourcePath { get; private set; }

        public static void Main(string[] args)
        {
            ConfigureLogger();
            
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
        
#if DEBUG
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
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
                var engine = new Localizer(_modFilePath, SourcePath, RunningMode.Dump, _language);
                engine.Run();

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
                    _mode = !string.Equals(modeOpt.Value(), "patch", StringComparison.OrdinalIgnoreCase) ?
                        RunningMode.Dump : RunningMode.Patch;
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
            if (string.IsNullOrWhiteSpace(SourcePath) && _mode == RunningMode.Patch)
            {
                Logger.Error(Strings.NoSourceFolderSpecified);
                return false;
            }

            return true;
        }

        private static void ConfigureLogger()
        {
            const string layout = "${level}\t|${logger}\t|${message}\t";

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
