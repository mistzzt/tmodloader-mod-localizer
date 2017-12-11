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
    }
}
