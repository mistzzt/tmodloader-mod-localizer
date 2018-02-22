using System;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Mod.Localizer.Emit.Provider;

namespace Mod.Localizer.Emit
{
    public abstract class Emitter
    {
        protected MethodDef Method { get; }

        protected ITranslationBaseProvider Provider { get; }

        protected Emitter(MethodDef method, ITranslationBaseProvider provider)
        {
            Method = method ?? throw new ArgumentNullException(nameof(method));
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public abstract void Emit(Instruction target, string value);

        public abstract bool IsTarget(Instruction instruction);
    }
}
