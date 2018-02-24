using System;
using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Mod.Localizer.ContentFramework;
using Mod.Localizer.Emit;
using Mod.Localizer.Extensions;

namespace Mod.Localizer.ContentProcessor
{
    public sealed class TranslationProcessor : Processor<TranslationContent>
    {
        public TranslationProcessor(TmodFileWrapper.ITmodFile modFile, ModuleDef modModule) : base(modFile, modModule)
        {
        }

        public override IReadOnlyList<TranslationContent> DumpContents()
        {
            var contents = new List<TranslationContent>();

            foreach (var method in ModModule.Types.SelectMany(x => x.Methods).Where(x => x.HasBody))
            {
                foreach (var instruction in method.Body.Instructions.Where(
                                x => x.OpCode == OpCodes.Call &&
                                x.Operand is IMethodDefOrRef m &&
                                m.IsMethod(nameof(Terraria.ModLoader.Mod),
                                           nameof(Terraria.ModLoader.Mod.CreateTranslation))))
                {
                    var key = (string)method.Body.FindStringLiteralBefore(instruction)?.Operand;
                    var value = (string)method.Body.FindStringLiteralAfter(instruction)?.Operand;

                    if (key == null || value == null)
                    {
                        continue;
                    }

                    contents.Add(new TranslationContent(method.DeclaringType) { Key = key, Value = value });
                }
            }

            return contents;
        }

        public override void PatchContents(IReadOnlyList<TranslationContent> contents)
        {
            foreach (var method in ModModule.Types.SelectMany(x => x.Methods).Where(x => x.HasBody))
            {
                var emitter = new LocalEmitter(method, Provider);

                var instructions = method.Body.Instructions;
                for (var i = 0; i < instructions.Count; i++)
                {
                    var instruction = instructions[i];
                    if (instruction.OpCode != OpCodes.Call || !(instruction.Operand is IMethodDefOrRef m) ||
                        !m.IsMethod(
                            nameof(Terraria.ModLoader.Mod),
                            nameof(Terraria.ModLoader.Mod.CreateTranslation)))
                    {
                        continue;
                    }

                    var stloc = instructions[i + 1];
                    if (!stloc.IsStloc())
                    {
                        continue;
                    }

                    var key = (string)method.Body.FindStringLiteralBefore(instruction)?.Operand;
                    if (key == null)
                    {
                        continue;
                    }

                    var content = contents.SingleOrDefault(x => string.Equals(key, x.Key, StringComparison.Ordinal));
                    if (content == null)
                    {
                        continue;
                    }

                    emitter.Emit(stloc, content.Value);
                }
            }
        }
    }
}
