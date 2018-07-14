using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using dnlib.DotNet;
using Mod.Localizer.ContentFramework;
using Mod.Localizer.Extensions;
using Terraria.ModLoader;

namespace Mod.Localizer.ContentProcessor
{
    public sealed class BuffProcessor : Processor<BuffContent>
    {
        protected override bool Selector(TypeDef type)
        {
            return type.HasBaseType(typeof(ModBuff).FullName);
        }

        [ProcessTarget(nameof(ModBuff.SetDefaults), nameof(BuffContent.Name), nameof(BuffContent.Tip))]
        public TargetInstruction[] SetDefault(MethodDef method)
        {
            var targets = new TargetInstruction[2];

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
                        targets[0] = new TargetInstruction
                        {
                            ReplaceTarget = source,
                            Value = value
                        };
                        break;
                    case "get_Description":
                        targets[1] = new TargetInstruction
                        {
                            ReplaceTarget = source,
                            Value = value
                        };
                        break;
                }
            }

            return targets;
        }

        public BuffProcessor(Localizer localizer, ModuleDef modModule) : base(localizer, modModule)
        {
        }
    }
}
