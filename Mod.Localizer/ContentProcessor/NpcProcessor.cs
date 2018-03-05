using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Mod.Localizer.ContentFramework;
using Mod.Localizer.Extensions;
using Terraria.ModLoader;

namespace Mod.Localizer.ContentProcessor
{
    public sealed class NpcProcessor : Processor<NpcContent>
    {
        protected override bool Selector(TypeDef type)
        {
            return type.HasBaseType(typeof(ModNPC).FullName);
        }

        [ProcessTarget(nameof(ModNPC.SetStaticDefaults), nameof(NpcContent.Name))]
        public TargetInstruction[] SetDefault(MethodDef method)
        {
            foreach (var instruction in method.Body.Instructions.Where(
                i => i.Operand is IMethodDefOrRef m && 
                     m.IsMethod(nameof(ModTranslation), nameof(ModTranslation.SetDefault))))
            {
                var source = method.Body.FindObjectInstance(instruction);
                var value = method.Body.FindStringLiteralBefore(instruction);
                if (source == null || value == null)
                {
                    continue;
                }

                switch (((IMethodDefOrRef)source.Operand).Name)
                {
                    case "get_DisplayName":
                        return new[]
                        {
                            new TargetInstruction
                            {
                                ReplaceTarget = source,
                                Value = value
                            }
                        };
                }
            }

            return new TargetInstruction[0];
        }

        [ProcessTarget(nameof(ModNPC.GetChat), nameof(NpcContent.ChatTexts))]
        public TargetInstruction[] ChatTexts(MethodDef method)
        {
            return method.Body.Instructions
                .Where(x => x.OpCode == OpCodes.Ldstr)
                .Select(x => new TargetInstruction(x))
                .ToArray();
        }

        [ProcessTarget(nameof(ModNPC.SetChatButtons), nameof(NpcContent.ShopButton1), nameof(NpcContent.ShopButton2))]
        public TargetInstruction[] ChatButton(MethodDef method)
        {
            var targets = new TargetInstruction[2];

            var instructions = method.Body.Instructions;

            for (var index = 0; index < instructions.Count; index++)
            {
                var ldstr = instructions[index];

                if (ldstr.OpCode != OpCodes.Ldstr)
                    continue;

                // arg_1: shop button 1
                // arg_2: shop button 2
                // value is assigned to the byRef parameter
                var ldarg = instructions.ElementAtOrDefault(index - 1);
                if (ldarg == null)
                    continue;

                if (ldarg.OpCode.Equals(OpCodes.Ldarg_1))
                    targets[0] = new TargetInstruction(ldstr);
                else if (ldarg.OpCode.Equals(OpCodes.Ldarg_2))
                    targets[1] = new TargetInstruction(ldstr);
            }

            return targets;
        }

        [ProcessTarget(nameof(ModNPC.TownNPCName), nameof(NpcContent.TownNpcNames))]
        public TargetInstruction[] TownName(MethodDef method)
        {
            return method.Body.Instructions
                .Where(x => x.OpCode == OpCodes.Ldstr)
                .Select(instruction => new TargetInstruction(instruction))
                .ToArray();
        }

        public NpcProcessor(TmodFileWrapper.ITmodFile modFile, ModuleDef modModule, GameCultures culture) : base(modFile, modModule, culture)
        {
        }
    }
}
