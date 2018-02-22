using dnlib.DotNet.Emit;

namespace Mod.Localizer.ContentProcessor
{
    public struct TargetInstructionArray
    {
        public Instruction[] Values { get; set; }

        public Instruction[] ReplaceTargets { get; set; }
    }
}
