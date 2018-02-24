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

            // insert after the target
            var index = Method.Body.Instructions.IndexOf(target) + 1;
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
