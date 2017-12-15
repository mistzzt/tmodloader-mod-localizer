using System;
using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using ModLocalizer.Framework;

namespace ModLocalizer.Extensions
{
    internal static class InstructionIterationExtensions
    {
        public static void ApplyPatch(this MethodDef method, Func<Instruction, bool> predicate,
            TranslationEmitter emitter, ITranslation translation, string propertyName)
        {
            method.ApplyPatch((inst, index) => predicate(inst[index]), emitter, translation, propertyName);
        }

        public static void ApplyPatch(this MethodDef method, Func<IList<Instruction>, int, bool> predicate,
            TranslationEmitter emitter, ITranslation translation, string propertyName)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            if (emitter == null) throw new ArgumentNullException(nameof(emitter));
            if (translation == null) throw new ArgumentNullException(nameof(translation));
            if (propertyName == null) throw new ArgumentNullException(nameof(propertyName));

            // check method body
            if (method?.HasBody != true) return;

            var property = translation.GetType().GetProperty(propertyName);
            if (!(property?.GetMethod.Invoke(translation, new object[0]) is IList<string> list))
                throw new ArgumentOutOfRangeException(nameof(propertyName));

            var source = method.Body.Instructions;
            for (int index = 0, listIndex = 0; index < source.Count; index++)
            {
                var instruction = source[index];
                if (!predicate(source, index))
                    continue;

                if (listIndex == list.Count)
                {
                    Console.WriteLine(DefaultConfigurations.LocalizerWarns.UnmatchedListCount);
                    break;
                }

                emitter.Emit(method, instruction, list[listIndex++]);
            }
        }
    }
}
