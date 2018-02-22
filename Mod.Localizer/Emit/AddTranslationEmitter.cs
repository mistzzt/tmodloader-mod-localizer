using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Mod.Localizer.Emit.Provider;
using Mod.Localizer.Extensions;

namespace Mod.Localizer.Emit
{
    public sealed class AddTranslationEmitter : Emitter
    {
        public AddTranslationEmitter(MethodDef method, ITranslationBaseProvider provider) : base(method, provider)
        {
        }

        public override void Emit(Instruction target, string value)
        {
            Method.Body.AppendLast(new[]
            {
                OpCodes.Ldarg_0.ToInstruction(),
                OpCodes.Call.ToInstruction(target.Operand as IMethodDefOrRef),
                OpCodes.Ldsfld.ToInstruction(Provider.GameCultureField),
                OpCodes.Ldstr.ToInstruction(value),
                OpCodes.Callvirt.ToInstruction(Provider.AddTranslationMethod)
            });
        }

        public override bool IsTarget(Instruction instruction)
        {
            // Process all instructions that call method with `ModTranslation` as return values
            // check opcode first
            if (instruction.OpCode != OpCodes.Call)
            {
                return false;
            }

            // check method return type
            var retType = ((IMethodDefOrRef)instruction.Operand).MethodSig.RetType;
            return retType.TryGetTypeRef() == Provider.ModTranslationType;
        }
    }
}
