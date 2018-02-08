using System;
using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet.Emit;

namespace ModLocalizer.Extensions
{
    internal static class InstructionExtensions
    {
        /// <summary>
        /// Inserts a group of instructions after the target instruction
        /// </summary>
        public static void Insert(this IList<Instruction> inst, int index, IEnumerable<Instruction> instructions)
        {
            foreach (var instruction in instructions.Reverse())
            {
                inst.Insert(index, instruction);
            }
        }

        /// <summary>
        /// Inserts a group of instructions into given instructions at the arbitrary last.
        /// </summary>
        public static void AppendLast(this IList<Instruction> inst, IEnumerable<Instruction> instructions)
        {
            // create a copy of instructions to be inserted
            var list = instructions.ToList();
            if (!list.Any())
            {
                return;
            }

            // valid target method should have `ret` as last instruction
            var retInst = inst.Last();
            if (retInst.OpCode != OpCodes.Ret)
            {
                throw new ArgumentOutOfRangeException(nameof(inst), "Invalid method instructions.");
            }

            // alter the last instruction of the target
            var first = list.First();
            retInst.OpCode = first.OpCode;
            retInst.Operand = first.Operand;

            // prepare instruction list
            list.Remove(first);
            list.Add(OpCodes.Ret.ToInstruction());

            // insert into the target
            foreach (var instruction in list)
            {
                inst.Add(instruction);
            }
        }
    }
}
