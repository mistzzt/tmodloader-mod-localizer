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
    public sealed class MiscTextProcessor : Processor<MiscContent>
    {
        public override void PatchContents(IReadOnlyList<MiscContent> contents)
        {
            foreach (var content in contents)
            {
                var typeName = $"{content.Namespace}.{content.TypeName}";

                var method = ModModule.Find(typeName, false)?.FindMethod(content.Method);
                if (method?.HasBody != true)
                {
                    continue;
                }

                var literalEmitter = new LiteralEmitter(method, Provider);

                var instructions = method.Body.Instructions;
                for (int index = 0, listIndex = 0; index < instructions.Count && listIndex < content.Contents.Count; index++)
                {
                    var instruction = instructions[index];
                    if (instruction.OpCode != OpCodes.Call || !(instruction.Operand is IMethodDefOrRef m) ||
                        !string.Equals(m.Name, "NewText", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    // `Main.NewText` parameters
                    instruction = instructions[index - 5];

                    if (instruction.OpCode.Equals(OpCodes.Ldstr))
                    {
                        literalEmitter.Emit(instruction, content.Contents[listIndex++]);
                    }
                    else if (instruction.OpCode.Equals(OpCodes.Call) &&
                             instruction.Operand is MemberRef n &&
                             string.Equals(n.Name, nameof(string.Concat), StringComparison.Ordinal))
                    {
                        var list = method.Body.FindStringLiteralsOf(instruction);

                        // the list given above is in reverse order
                        foreach (var ldstr in list.Reverse())
                        {
                            literalEmitter.Emit(ldstr, content.Contents[listIndex++]);

                            if (listIndex >= content.Contents.Count)
                            {
                                break;
                            }
                        }
                    }
                }
            }
        }

        public override IReadOnlyList<MiscContent> DumpContents()
        {
            var miscs = new List<MiscContent>();

            foreach (var method in ModModule.Types
                .SelectMany(x => x.Methods)
                .Where(x => x.HasBody))
            {
                var text = new MiscContent(method);

                var instructions = method.Body.Instructions;
                for (var index = 0; index < instructions.Count; index++)
                {
                    var instruction = instructions[index];
                    if (instruction.OpCode != OpCodes.Call || !(instruction.Operand is IMethodDefOrRef m) ||
                        !string.Equals(m.Name, "NewText", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    // `Main.NewText` parameters
                    instruction = instructions[index - 5];

                    if (instruction.OpCode.Equals(OpCodes.Ldstr))
                    {
                        //result.Add(new TargetInstruction(ins));
                        text.Contents.Add((string)instruction.Operand);
                    }
                    else if (instruction.OpCode.Equals(OpCodes.Call) &&
                             instruction.Operand is MemberRef n &&
                             string.Equals(n.Name, nameof(string.Concat), StringComparison.Ordinal))
                    {
                        var list = method.Body.FindStringLiteralsOf(instruction);

                        // the list given above is in reverse order
                        foreach (var ldstr in list.Reverse())
                        {
                            text.Contents.Add((string)ldstr.Operand);
                        }
                    }
                }

                if (text.Contents.Any())
                {
                    miscs.Add(text);
                }
            }

            return miscs;
        }

        public MiscTextProcessor(Localizer localizer, ModuleDef modModule) : base(localizer, modModule)
        {
        }
    }
}
