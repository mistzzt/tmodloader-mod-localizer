using System;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Mod.Localizer.Emit.Provider;

namespace Mod.Localizer.Emit
{
    public sealed class LocalEmitter : Emitter
    {
        public LocalEmitter(MethodDef method, ITranslationBaseProvider provider) : base(method, provider)
        {
        }

        public override void Emit(Instruction target, string value)
        {
            throw new NotImplementedException();
        }

        public override bool IsTarget(Instruction instruction)
        {
            return false;
        }
    }
}
