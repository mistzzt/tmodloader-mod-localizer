using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
			Directory.CreateDirectory(GetPath("Buffs"));
			Directory.CreateDirectory(GetPath("Miscs"));
			Directory.CreateDirectory(GetPath("Tiles"));

			LoadAssembly();
			DumpBuildProperties();
			DumpTmodProperties();
			DumpItems();
			DumpNpcs();
			DumpBuffs();
			DumpMiscs();
			DumpMapEntries();
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

			WriteFiles(items, "Items");
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

			WriteFiles(npcs, "NPCs");
		}

		private void DumpBuffs()
		{
			var buffs = new List<BuffTranslation>();

			foreach (var type in _module.Types.Where(t => t.BaseType?.Name.ToString().Equals("ModBuff", StringComparison.Ordinal) == true))
			{
				var buff = new BuffTranslation { TypeName = type.Name, Namespace = type.Namespace };

				var method = type.FindMethod("SetDefaults", MethodSig.CreateInstance(_module.CorLibTypes.Void));

				if (method?.HasBody != true)
					continue;

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
								buff.Name = value;
								break;
							case "get_Description":
								buff.Tip = value;
								break;
						}
					}
				}

				buffs.Add(buff);
			}

			WriteFiles(buffs, "Buffs");
		}

		private void DumpMiscs()
		{
			var miscs = new List<MiscTranslation>();

			foreach (var type in _module.Types)
			{
				foreach (var method in type.Methods)
				{
					if (!method.HasBody)
						continue;

					var inst = method.Body.Instructions;

					var misc = new MiscTranslation { TypeName = type.Name, Namespace = type.Namespace, Method = method.Name };
					var write = false;

					for (var index = 0; index < inst.Count; index++)
					{
						var ins = inst[index];

						if (!ins.OpCode.Equals(OpCodes.Call) || !(ins.Operand is IMethodDefOrRef m) ||
							!m.Name.ToString().Equals("NewText", StringComparison.Ordinal))
							continue;

						if ((ins = inst[index - 5]).OpCode.Equals(OpCodes.Ldstr))
						{
							misc.Contents.Add(ins.Operand as string);
							write = true;
						}
						else if (ins.OpCode.Equals(OpCodes.Call) &&
								 ins.Operand is IMethodDefOrRef n &&
								 n.Name.ToString().Equals("Concat", StringComparison.Ordinal))
						{
							var index2 = index;
							var count = 0;
							var total = n.MethodSig.Params.Count;
							var list = new List<string>();
							while (--index2 > 0 && count < total)
							{
								ins = inst[index2];
								if (ins.OpCode.Equals(OpCodes.Ldelem_Ref)) // for array
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
							misc.Contents.AddRange(list);
							write = true;
						}
					}

					if (write)
						miscs.Add(misc);
				}
			}

			WriteFiles(miscs, "Miscs");
		}

		private void DumpMapEntries()
		{
			var entries = new List<MapEntryTranslation>();

			foreach (var type in _module.Types.Where(t => t.BaseType?.Name.ToString().Equals("ModTile", StringComparison.Ordinal) == true))
			{
				var entry = new MapEntryTranslation { TypeName = type.Name, Namespace = type.Namespace };

				var method = type.FindMethod("SetDefaults", MethodSig.CreateInstance(_module.CorLibTypes.Void));

				if (method?.HasBody != true)
					continue;

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
						entry.Name = value;
					}
				}

				entries.Add(entry);
			}

			WriteFiles(entries, "Tiles");
		}

		private string GetPath(params string[] paths) => Path.Combine(_mod.Name, Path.Combine(paths));

		public void WriteFiles<T>(IList<T> translations, string category) where T : ITranslation
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