using System;
using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;
using Mod.Localizer.ContentFramework;
using Mod.Localizer.Resources;
using NLog;

namespace Mod.Localizer.ContentProcessor
{
    public abstract class Processor<T> where T : Content
    {
        protected readonly TmodFileWrapper.ITmodFile ModFile;

        protected readonly ModuleDef ModModule;

        protected readonly Logger Logger = LogManager.GetCurrentClassLogger();

        protected Processor(TmodFileWrapper.ITmodFile modFile, ModuleDef modModule)
        {
            ModFile = modFile ?? throw new ArgumentNullException(nameof(modFile));
            ModModule = modModule ?? throw new ArgumentNullException(nameof(modModule));
        }

        public virtual IReadOnlyList<T> DumpContents()
        {
            var contents = new List<T>();

            foreach (var type in ModModule.Types.Where(Selector))
            {
                try
                {
                    contents.Add(DumpContent(type));
                }
                catch (Exception ex)
                {
                    Logger.Error(Strings.DumpExceptionOccur, type.FullName);
                    Logger.Error(ex);
                }
            }

            return contents.AsReadOnly();
        }

        public virtual void PatchContents(IReadOnlyList<T> contents)
        {
            foreach (var content in contents)
            {
                // convert to full name for historical reasons :(
                var fullName = $"{content.Namespace}.{content.TypeName}";

                var type = ModModule.Find(fullName, true);
                if (type != null)
                {
                    try
                    {
                        PatchContent(type, content);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(Strings.PatchExceptionOccur, type.FullName);
                        Logger.Error(ex);
                    }
                }
                else
                {
                    Logger.Warn(Strings.InvalidContent, fullName);
                }
            }
        }

        protected abstract T DumpContent(TypeDef type);

        protected abstract void PatchContent(TypeDef type, T content);

        protected virtual bool Selector(TypeDef type)
        {
            throw new NotImplementedException();
        }
    }
}
