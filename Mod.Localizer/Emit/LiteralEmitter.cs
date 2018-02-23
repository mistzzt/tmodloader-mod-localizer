using System;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Mod.Localizer.Emit.Provider;

namespace Mod.Localizer.Emit
{
    public sealed class LiteralEmitter : Emitter
    {
        public LiteralEmitter(MethodDef method, ITranslationBaseProvider provider) : base(method, provider)
        {
        }

        public override void Emit(Instruction target, string value)
        {
            // replace the string loaded with a call to `GetTextValue` of translation

            // first replace the string literal to translation key
            var key = Provider.CreateTranslation(Method.DeclaringType.Name, (string)target.Operand);
            target.Operand = Provider.ToGameLocalizationKey(key);

            // set translated value
            Provider.AddTranslation(key, value);

            // insert the static method call right after the target instruction
            var index = Method.Body.Instructions.IndexOf(target);
            if (index == -1) throw new ArgumentOutOfRangeException(nameof(target));
            Method.Body.Instructions.Insert(index + 1, OpCodes.Call.ToInstruction(Provider.GetTextValueMethod));

            // simplify branches
            Method.Body.SimplifyBranches();
            Method.Body.OptimizeBranches();
        }

        public override bool IsTarget(Instruction instruction)
        {
            return instruction.OpCode == OpCodes.Ldstr;
        }
    }
}
