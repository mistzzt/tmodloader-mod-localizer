using System;
using System.IO;
using Microsoft.Extensions.CommandLineUtils;
using ModLocalizer.ModLoader;

namespace ModLocalizer
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			if (args.Length == 0)
			{
				args = new[]
				{
#if DEBUG
					@"..\..\..\..\..\ExampleMod.tmod",
					"-m",
					"patch",
					"-f",
					"ExampleMod"
#else
					"--help"
#endif
				};

			}

			var app = new CommandLineApplication(false)
			{
				FullName = typeof(Program).Namespace,
				ShortVersionGetter = () => typeof(Program).Assembly.GetName().Version.ToString(2),
				LongVersionGetter = () => typeof(Program).Assembly.GetName().Version.ToString(3)
			};

			var version = typeof(Program).Assembly.GetName().Version;
			app.HelpOption("--help | -h");
			app.VersionOption("-v | --version", version.ToString(2), version.ToString(3));

			var pathArgument = app.Argument("Path", "The location of mod to be patched OR dumped");
			var modeOption = app.Option("-m | --mode", "Set program mode: DUMP mod content or PATCH content to mod", CommandOptionType.SingleValue);
			var folderOption = app.Option("-f | --folder", "Set the folder of localized content for PATCHING mod", CommandOptionType.SingleValue);
			var languageOption = app.Option("-l | --language", "Default: Chinese. Wrong language inputted may cause mod loading FAILURE!!", CommandOptionType.SingleValue);

			var dump = true;
			string folder = null, path = null, language = "Chinese";

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
					Console.WriteLine("Please specify the mod file to be processed.");
					Environment.Exit(1);
				}

				if (!dump && string.IsNullOrWhiteSpace(folder))
				{
					Console.WriteLine("Please specify the content folder for mod patching.");
					Environment.Exit(1);
				}

				return 0;
			});
			app.Execute(args);

			if (!string.IsNullOrWhiteSpace(path))
			{
				ProcessInput(path, folder, dump, language);
			}
		}

		private static void ProcessInput(string modPath, string contentFolderPath, bool dump = true, string language = "Chinese")
		{
			if (string.IsNullOrWhiteSpace(modPath))
				throw new ArgumentException("Value cannot be null or whitespace.", nameof(modPath));

			if (!dump && string.IsNullOrWhiteSpace(contentFolderPath))
				throw new ArgumentException("Value cannot be null or whitespace.", nameof(contentFolderPath));

			if (!File.Exists(modPath))
			{
				Console.WriteLine("mod file does not exist");
				return;
			}

			var modFile = new TmodFile(modPath);
			modFile.Read();

			if (dump)
			{
				new Dumper(modFile).Run();
			}
			else
			{
				new Patcher(modFile, contentFolderPath, language).Run();
			}
		}
	}
}
