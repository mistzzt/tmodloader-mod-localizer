using System;
using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Mod.Localizer.ContentFramework;
using Mod.Localizer.Extensions;

namespace Mod.Localizer.ContentProcessor
{
    public sealed class ItemProcessor : Processor<ItemContent>
    {
        public ItemProcessor(TmodFileWrapper.ITmodFile modFile, ModuleDef modModule) : base(modFile, modModule)
        {
        }

        protected override ItemContent DumpContent(TypeDef type)
        {
            var content = new ItemContent(type);

            var method = type.FindMethod("SetStaticDefaults", MethodSig.CreateInstance(ModModule.CorLibTypes.Void));
            if (method?.HasBody == true)
            {
                var inst = method.Body.Instructions;

                for (var index = 0; index < inst.Count; index++)
                {
                    var ins = inst[index];

                    if (ins.OpCode != OpCodes.Ldstr)
                        continue;

                    var value = ins.Operand as string;

                    ins = inst[++index];

                    if (ins.Operand is IMethodDefOrRef m &&
                        string.Equals(m.Name.ToString(), "SetDefault") &&
                        string.Equals(m.DeclaringType.Name, "ModTranslation", StringComparison.Ordinal))
                    {
                        ins = inst[index - 2];

                        if (!(ins?.Operand is IMethodDefOrRef propertyGetter))
                        {
                            // some translation objects may get from stack;
                            // In this case, we can't know their type. skip
                            continue;
                        }

                        switch (propertyGetter.Name)
                        {
                            case "get_Tooltip":
                                content.ToolTip = value;
                                break;
                            case "get_DisplayName":
                                content.Name = value;
                                break;
                        }
                    }
                }
            }

            method = type.FindMethod("ModifyTooltips");
            if (method?.HasBody == true)
            {
                var inst = method.Body.Instructions;

                for (var index = 0; index < inst.Count; index++)
                {
                    var ins = inst[index];

                    if (ins.OpCode != OpCodes.Newobj || !(ins.Operand is MemberRef m) || !m.DeclaringType.Name.Equals("TooltipLine"))
                        continue;

                    ins = inst[index - 1];

                    if (ins.OpCode.Equals(OpCodes.Ldstr) && inst[index - 2].OpCode.Equals(OpCodes.Ldstr))
                    {
                        content.ModifyTooltips.Add(inst[index - 2].Operand as string);
                        content.ModifyTooltips.Add(inst[index - 1].Operand as string);
                    }
                    else if (ins.OpCode.Equals(OpCodes.Call) && ins.Operand is MemberRef n && n.Name.Equals("Concat"))
                    {
                        var index2 = index;
                        var count = 0;
                        var total = n.MethodSig.Params.Count + 1;
                        var list = new List<string>();
                        while (--index2 > 0 && count < total)
                        {
                            ins = inst[index2];
                            if (ins.OpCode.Equals(OpCodes.Ldelem_Ref))
                            {
                                count++;
                            }
                            else if (ins.OpCode.Equals(OpCodes.Ldstr))
                            {
                                count++;
                                list.Add(ins.Operand as string);
                            }
                        }
                        list.Reverse();

                        foreach (var line in list)
                        {
                            content.ModifyTooltips.Add(line);
                        }
                    }
                }
            }

            method = type.FindMethod("UpdateArmorSet");
            if (method?.HasBody == true)
            {
                var inst = method.Body.Instructions;

                for (var index = 0; index < inst.Count; index++)
                {
                    var ins = inst[index];

                    if (ins.OpCode != OpCodes.Ldstr)
                        continue;

                    var value = ins.Operand as string;

                    if ((ins = inst[++index]).OpCode == OpCodes.Stfld && ins.Operand is MemberRef m)
                    {
                        switch (m.Name)
                        {
                            case "setBonus":
                                content.SetBonus = value;
                                break;
                        }
                    }
                }
            }

            return content;
        }

        protected override void PatchContent(TypeDef type, ItemContent content)
        {
            throw new NotImplementedException();
        }

        protected override bool Selector(TypeDef type)
        {
            return type.HasBaseType("Terraria.ModLoader.ModItem");
        }
    }
}
