using System;
using System.IO;
using Mod.Localizer.ContentFramework;
using Mod.Localizer.ContentProcessor;

namespace Mod.Localizer.Extensions
{
    internal static class ProcessorExtensions
    {
        public static string GetExtraDataPath<T>(this Processor<T> processor) where T : Content
        {
            if (!DefaultConfigurations.FileMapper.ContainsKey(processor.GetType()))
            {
                throw new InvalidOperationException($"{typeof(T).FullName} does not have an extra file.");
            }

            return Path.Combine(Program.SourcePath, DefaultConfigurations.FileMapper[processor.GetType()]);
        }
    }
}
