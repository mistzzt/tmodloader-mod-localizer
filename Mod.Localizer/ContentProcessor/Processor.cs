using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Mod.Localizer.ContentFramework;
using Mod.Localizer.Emit;
using Mod.Localizer.Emit.Provider;
using Mod.Localizer.Resources;
using NLog;

namespace Mod.Localizer.ContentProcessor
{
    public abstract class Processor<T> where T : Content
    {
        protected readonly Logger Logger;
        protected readonly ModuleDef ModModule;
        protected readonly TmodFileWrapper.ITmodFile ModFile;
        protected readonly ITranslationBaseProvider Provider;
        protected readonly IDictionary<string, MethodInfo> InstructionSelectors;

        protected Processor(TmodFileWrapper.ITmodFile modFile, ModuleDef modModule)
        {
            ModFile = modFile ?? throw new ArgumentNullException(nameof(modFile));
            ModModule = modModule ?? throw new ArgumentNullException(nameof(modModule));

            Logger = LogManager.GetLogger("Proc." + GetType().Name);

            InstructionSelectors = new Dictionary<string, MethodInfo>();

            Provider = new HardCodedTranslationProvider(modFile, modModule, GameCultures.Chinese);

            InitializeInstructionSelectors();
        }

        public virtual IReadOnlyList<T> DumpContents()
        {
            var contents = ModModule.Types.Where(Selector).Select(DumpContent).ToList();

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

        protected virtual T DumpContent(TypeDef type)
        {
            var content = (T)Activator.CreateInstance(typeof(T), type);

            foreach (var kvp in InstructionSelectors)
            {
                var properties = kvp.Value.GetCustomAttribute<ProcessTargetAttribute>();

                var methodDef = type.FindMethod(kvp.Key);
                if (methodDef?.HasBody != true)
                {
                    // not every target type has all possible properties to be translated
                    continue;
                }

                // skip empty array
                var result = (TargetInstruction[])kvp.Value.Invoke(this, new object[] { methodDef });
                if (result.Length == 0)
                {
                    continue;
                }

                for (var index = 0; index < properties.Value.Length; index++)
                {
                    // skip null element
                    if (result[index].Value == null)
                    {
                        continue;
                    }

                    var prop = typeof(T).GetProperty(properties.Value[index]);
                    if (prop == null)
                    {
                        throw new NotSupportedException();
                    }

                    if (prop.PropertyType == typeof(string))
                    {
                        prop.SetValue(content, (string)result[index].Value.Operand ?? string.Empty);
                    }
                    else
                    {
                        // expected to be the last key
                        var list = (IList<string>)prop.GetValue(content);
                        for (var i = index; i < result.Length; i++)
                        {
                            list.Add((string)result[i].Value.Operand);
                        }

                        break;
                    }
                }
            }

            return content;
        }

        protected virtual void PatchContent(TypeDef type, T content)
        {
            foreach (var kvp in InstructionSelectors)
            {
                var properties = kvp.Value.GetCustomAttribute<ProcessTargetAttribute>();

                var methodDef = type.FindMethod(kvp.Key);
                if (methodDef?.HasBody != true)
                {
                    // not every target type has all possible properties to be translated
                    continue;
                }

                var emitters = Emitter.LoadEmitters(methodDef, Provider);

                // skip empty array
                var result = (TargetInstruction[])kvp.Value.Invoke(this, new object[] { methodDef });
                if (result.Length == 0)
                {
                    continue;
                }

                for (var index = 0; index < properties.Value.Length; index++)
                {
                    // skip null element
                    if (result[index].ReplaceTarget == null)
                    {
                        continue;
                    }

                    var prop = typeof(T).GetProperty(properties.Value[index]);
                    if (prop == null)
                    {
                        throw new NotSupportedException();
                    }

                    if (prop.PropertyType == typeof(string))
                    {
                        Patch(result[index].ReplaceTarget, (string)prop.GetValue(content), emitters);
                    }
                    else
                    {
                        // expected to be the last key
                        var list = (IList<string>)prop.GetValue(content);
                        for (int i = index, listIndex = 0; i < result.Length; i++, listIndex++)
                        {
                            Patch(result[i].ReplaceTarget, list[listIndex], emitters);
                        }

                        break;
                    }
                }
            }

            void Patch(Instruction instruction, string value, IReadOnlyList<Emitter> emitters)
            {
                var emitter = emitters.FirstOrDefault(x => x.IsTarget(instruction));
                if (emitter == null)
                {
                    return;
                }

                emitter.Emit(instruction, value);
            }
        }

        protected virtual bool Selector(TypeDef type)
        {
            throw new NotImplementedException();
        }

        private void InitializeInstructionSelectors()
        {
            var methods = GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(
                    x => !x.IsAbstract &&
                          x.ReturnType == typeof(TargetInstruction[]) &&
                          x.GetCustomAttribute<ProcessTargetAttribute>() != null);

            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<ProcessTargetAttribute>();

                InstructionSelectors.Add(attr.Method, method);
            }
        }
    }
}
