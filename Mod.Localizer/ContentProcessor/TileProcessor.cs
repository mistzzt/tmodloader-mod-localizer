using System.Linq;
using dnlib.DotNet;
using Mod.Localizer.ContentFramework;
using Mod.Localizer.Extensions;
using Terraria.ModLoader;

namespace Mod.Localizer.ContentProcessor
{
    public sealed class TileProcessor : Processor<TileContent>
    {
        public TileProcessor(TmodFileWrapper.ITmodFile modFile, ModuleDef modModule) : base(modFile, modModule)
        {
        }

        protected override bool Selector(TypeDef type)
        {
            return type.HasBaseType(typeof(ModTile).FullName);
        }

        [ProcessTarget(nameof(ModTile.SetDefaults), nameof(TileContent.Name))]
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

                return new[]
                {
                    new TargetInstruction
                    {
                        ReplaceTarget = source,
                        Value = value
                    }
                };
            }

            return new TargetInstruction[0];
        }
    }
}
