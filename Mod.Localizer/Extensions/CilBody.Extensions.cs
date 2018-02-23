using System;
using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Mod.Localizer.Extensions
{
    internal static class CilBodyExtensions
    {
        public static Instruction FindObjectInstance(this CilBody body, Instruction methodInvokeInstruction)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));
            if (methodInvokeInstruction == null) throw new ArgumentNullException(nameof(methodInvokeInstruction));

            if (!(methodInvokeInstruction.Operand is IMethodDefOrRef target)
                || !target.MethodSig.HasThis)
            {
                throw new ArgumentOutOfRangeException(nameof(methodInvokeInstruction));
            }

            // instance type is the declaring type when `HasThis` bit is set to true
            var type = target.DeclaringType;

            var instructions = body.Instructions;

            for (var index = instructions.IndexOf(methodInvokeInstruction); index > 0; index--)
            {
                var instruction = instructions[index];

                if (instruction.OpCode == OpCodes.Call)
                {
                    var method = (IMethodDefOrRef)instruction.Operand;
                    if (method.MethodSig.RetType.ToTypeDefOrRef() == type)
                    {
                        return instruction;
                    }
                }
                else if (instruction.IsLdloc())
                {
                    var local = instruction.GetLocal(body.Variables);
                    if (local.Type.ToTypeDefOrRef() == type)
                    {
                        var src = body.FindSource(local, instruction);
                        if (src != null)
                        {
                            return src;
                        }
                    }
                }
            }

            return null;
        }

        public static Instruction FindSource(this CilBody body, Local variable, Instruction ldloc)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));
            if (variable == null) throw new ArgumentNullException(nameof(variable));
            if (ldloc == null) throw new ArgumentNullException(nameof(ldloc));

            for (var index = body.Instructions.IndexOf(ldloc); index > 0; index--)
            {
                var instruction = body.Instructions[index];
                if (instruction.IsStloc() && instruction.GetLocal(body.Variables) == variable)
                {
                    return instruction;
                }
            }

            return null;
        }

        public static Instruction FindStringLiteralBefore(this CilBody body, Instruction target)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));
            if (target == null) throw new ArgumentNullException(nameof(target));

            var instructions = body.Instructions;

            for (var index = instructions.IndexOf(target); index > 0; index--)
            {
                var instruction = instructions[index];

                // only find string literal now
                if (instruction.OpCode == OpCodes.Ldstr)
                {
                    return instruction;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds string literals for calling `String.Concat`
        /// </summary>
        /// <param name="body">The method body to be searched</param>
        /// <param name="target">The target method call of `String.Concat`</param>
        /// <returns>The list of instructions</returns>
        public static IList<Instruction> FindStringLiteralsOf(this CilBody body, Instruction target)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));
            if (target == null) throw new ArgumentNullException(nameof(target));

            if (target.OpCode != OpCodes.Call || !(target.Operand is MemberRef n) || !n.Name.Equals("Concat"))
            {
                throw new ArgumentOutOfRangeException(nameof(target));
            }

            var ldstrs = new List<Instruction>();

            var instructions = body.Instructions;

            for (int i = instructions.IndexOf(target), total = n.MethodSig.Params.Count; i > 0 && total > 0; i--)
            {
                var instruction = instructions[i];
                if (instruction.OpCode.Equals(OpCodes.Ldelem_Ref))
                {
                    // skip array element loading
                    // see this pattern in Thorium mod
                    total--;
                }
                else if (instruction.OpCode.Equals(OpCodes.Ldstr))
                {
                    // In Thorium mod, mod player data sometimes would be loaded as tooltip;
                    // the method will be called with a unique id taken as parameter.
                    // we should skip adding the unique identifier.
                    if (instructions[i + 1].OpCode != OpCodes.Callvirt)
                    {
                        ldstrs.Add(instruction);
                    }

                    total--;
                }
            }

            return ldstrs;
        }

        /// <summary>
        /// Inserts a group of instructions into the method as the arbitrary last.
        /// </summary>
        public static void AppendLast(this CilBody body, IEnumerable<Instruction> instructions)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));
            if (instructions == null) throw new ArgumentNullException(nameof(instructions));

            var ret = body.Instructions.Last();
            if (ret == null || ret.OpCode != OpCodes.Ret)
            {
                throw new ArgumentOutOfRangeException(nameof(body));
            }

            var list = instructions.ToList();
            if (!list.Any())
            {
                return;
            }

            var first = list.First();
            ret.OpCode = first.OpCode;
            ret.Operand = first.Operand;

            list.Remove(first);
            list.Add(OpCodes.Ret.ToInstruction());

            foreach (var instruction in list)
            {
                body.Instructions.Add(instruction);
            }
        }

        public static int Next(this CilBody body, int index, Predicate<Instruction> predicate)
        {
            if (index < 0 || index >= body.Instructions.Count)
            {
                return -1;
            }

            for (; index < body.Instructions.Count; index++)
            {
                if (predicate(body.Instructions[index]))
                {
                    return index;
                }
            }

            return -1;
        }

        public static void Insert(this IList<Instruction> inst, int index, IEnumerable<Instruction> instructions)
        {
            foreach (var instruction in instructions.Reverse())
            {
                inst.Insert(index, instruction);
            }
        }
    }
}
