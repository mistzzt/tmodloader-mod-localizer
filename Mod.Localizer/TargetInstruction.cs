using dnlib.DotNet.Emit;

namespace Mod.Localizer
{
    public struct TargetInstruction
    {
        public Instruction Value { get; set; }

        public Instruction ReplaceTarget { get; set; }

        public TargetInstruction(Instruction same)
        {
            Value = ReplaceTarget = same;
        }
    }
}
