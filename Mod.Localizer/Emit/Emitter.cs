using System;
using System.Collections.Generic;
using System.Linq;
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

        public static IReadOnlyList<Emitter> LoadEmitters(MethodDef method, ITranslationBaseProvider provider)
        {
            var types = typeof(Emitter).Assembly.GetTypes().Where(x => x.BaseType == typeof(Emitter));

            return types.Select(type => (Emitter)Activator.CreateInstance(type, method, provider)).ToList();
        }
    }
}
