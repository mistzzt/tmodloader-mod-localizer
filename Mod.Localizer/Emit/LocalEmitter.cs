using System;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Mod.Localizer.Emit.Provider;
using Mod.Localizer.Extensions;

namespace Mod.Localizer.Emit
{
    public sealed class LocalEmitter : Emitter
    {
        public LocalEmitter(MethodDef method, ITranslationBaseProvider provider) : base(method, provider)
        {
        }

        public override void Emit(Instruction target, string value)
        {
            var local = target.GetLocal(Method.Body.Variables);
            if (local == null)
            {
                throw new ArgumentOutOfRangeException(nameof(target));
            }

            // target is expected to be `stloc.0` after the translation object
            // has been created. Here, we find the next `ldloc.0`
            var instructions = Method.Body.Instructions;
            var startIndex = instructions.IndexOf(target) + 1;
            var index = startIndex;
            while (instructions[index++].OpCode != OpCodes.Ldloc_0 && index < instructions.Count)
            {
                // empty
            }

            // if we didn't find the next `ldloc.0`, we use the original `stloc.0`
            if (index == instructions.Count)
            {
                index = startIndex;
            }
            else
            {
                index += 2;
            }

            Method.Body.Instructions.Insert(index, new[]
            {
                OpCodes.Ldloc_S.ToInstruction(local),
                OpCodes.Ldsfld.ToInstruction(Provider.GameCultureField),
                OpCodes.Ldstr.ToInstruction(value),
                OpCodes.Callvirt.ToInstruction(Provider.AddTranslationMethod)
            });

            Method.Body.SimplifyBranches();
            Method.Body.OptimizeBranches();
        }

        public override bool IsTarget(Instruction instruction)
        {
            return instruction.IsLdloc() || instruction.IsStloc();
        }
    }
}
