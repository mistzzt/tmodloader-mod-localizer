﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using ModLocalizer.Framework;
using ModLocalizer.ModLoader;
using Newtonsoft.Json;

namespace ModLocalizer
{
	internal sealed class Dumper
	{
		private readonly TmodFile _mod;

		private readonly byte[] _assembly;

		private ModuleDef _module;

		public Dumper(TmodFile mod)
		{
			_mod = mod;

			_assembly = _mod.GetMainAssembly(false);
		}

		public void Run()
		{
			Directory.CreateDirectory(_mod.Name);
			Directory.CreateDirectory(GetPath("Items"));
			Directory.CreateDirectory(GetPath("NPCs"));

			LoadAssembly();

			DumpBuildProperties();
			DumpTmodProperties();
			DumpItems();
			DumpNpcs();
		}

		private void LoadAssembly()
		{
			var assembly = AssemblyDef.Load(_assembly);

			_module = assembly.Modules.Single();
		}

		private void DumpBuildProperties()
		{
			var infoData = _mod.GetFile("Info");

			var properties = BuildProperties.ReadBytes(infoData);

			using (var fs = new FileStream(GetPath("Info.json"), FileMode.Create))
			{
				using (var sw = new StreamWriter(fs))
				{
					sw.Write(JsonConvert.SerializeObject(properties, Formatting.Indented));
				}
			}
		}

		private void DumpTmodProperties()
		{
			var properties = _mod.Properties;

			using (var fs = new FileStream(GetPath("ModInfo.json"), FileMode.Create))
			{
				using (var sw = new StreamWriter(fs))
				{
					sw.Write(JsonConvert.SerializeObject(properties, Formatting.Indented));
				}
			}
		}

		private void DumpItems()
		{
			var items = new List<ItemTranslation>();

			foreach (var type in _module.Types.Where(
				t => t.BaseType?.Name.ToString().Equals("ModItem", StringComparison.Ordinal) == true))
			{
				var item = new ItemTranslation { TypeName = type.Name, Namespace = type.Namespace };

				var method = type.FindMethod("SetStaticDefaults", MethodSig.CreateInstance(_module.CorLibTypes.Void));
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

							var propertyGetter = (IMethodDefOrRef)ins.Operand;
							switch (propertyGetter.Name)
							{
								case "get_Tooltip":
									item.ToolTip = value;
									break;
								case "get_DisplayName":
									item.Name = value;
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

						if (index - 1 <= 0) continue;

						ins = inst[index - 1];

						if (index - 2 > 0 && ins.OpCode.Equals(OpCodes.Ldstr) && inst[index - 2].OpCode.Equals(OpCodes.Ldstr))
						{
							item.ModifyTooltips.Add(inst[index - 2].Operand as string);
							item.ModifyTooltips.Add(inst[index - 1].Operand as string);
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
							item.ModifyTooltips.AddRange(list);
						}


					}
				}

				items.Add(item);
			}

			WriteFiles(items.ConvertAll(x => (ITranslation) x), "Items");
		}

		private void DumpNpcs()
		{
			var npcs = new List<NpcTranslation>();

			foreach (var type in _module.Types.Where(
				t => t.BaseType?.Name.ToString().Equals("ModNPC", StringComparison.Ordinal) == true))
			{
				var npc = new NpcTranslation { TypeName = type.Name, Namespace = type.Namespace };

				var method = type.FindMethod("SetStaticDefaults", MethodSig.CreateInstance(_module.CorLibTypes.Void));
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

							var propertyGetter = (IMethodDefOrRef)ins.Operand;
							switch (propertyGetter.Name)
							{
								case "get_DisplayName":
									npc.Name = value;
									break;
							}
						}
					}
				}

				method = type.FindMethod("GetChat");
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

						if ((ins.OpCode.Equals(OpCodes.Call) || ins.OpCode.Equals(OpCodes.Callvirt))
						    && ins.Operand is IMethodDefOrRef m)
							if (!m.Name.ToString().Equals("Concat") || m.MethodSig.Params.Count == 1)
								continue;

						npc.ChatTexts.Add(value);
					}
				}

				method = type.FindMethod("SetChatButtons");
				if (method?.HasBody == true)
				{
					var inst = method.Body.Instructions;

					for (var index = 0; index < inst.Count; index++)
					{
						var ins = inst[index];

						if (ins.OpCode != OpCodes.Ldstr)
							continue;

						var value = ins.Operand as string;

						ins = inst[index - 1];

						if (ins.OpCode.Equals(OpCodes.Ldarg_1))
							npc.ShopButton1 = value;
						else if (ins.OpCode.Equals(OpCodes.Ldarg_2))
							npc.ShopButton2 = value;
					}
				}

				npcs.Add(npc);
			}

			WriteFiles(npcs.ConvertAll(x => (ITranslation) x), "NPCs");
		}

		private string GetPath(params string[] paths) => Path.Combine(_mod.Name, Path.Combine(paths));

		public void WriteFiles(IList<ITranslation> translations, string category)
		{
			foreach (var ns in translations.Select(i => i.Namespace).Distinct())
			{
				using (var fs = File.Create(GetPath(category, ns + ".json")))
				{
					using (var sr = new StreamWriter(fs))
					{
						sr.Write(JsonConvert.SerializeObject(translations.Where(i => i.Namespace.Equals(ns)).ToList(), Formatting.Indented));
					}
				}
			}
		}
	}
}