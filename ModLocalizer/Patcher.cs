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
	internal sealed class Patcher
	{
		private readonly TmodFile _mod;
		private readonly string _contentPath;
		private readonly string _language;
		private readonly byte[] _assembly;
		private readonly byte[] _monoAssembly;

		private ModuleDef _module;
		private ModuleDef _monoModule;

		private TranslationEmitter _emitter;
		private TranslationEmitter _monoEmitter;

		public Patcher(TmodFile mod, string contentPath, string language)
		{
			_mod = mod;
			_contentPath = contentPath;
			_language = language;

			_assembly = _mod.GetMainAssembly(false);
			_monoAssembly = _mod.GetMainAssembly(true);
		}

		public void Run()
		{
			LoadAssemblies();

			ApplyBuildProperties();
			ApplyTmodProperties();

			ApplyItems();
			ApplyNpcs();
			ApplyBuffs();
			ApplyMiscs();
			ApplyMapEntries();
			ApplyCustomTranslations();

			Save();
		}

		private void LoadAssemblies()
		{
			_module = AssemblyDef.Load(_assembly).Modules.Single();
			_emitter = new TranslationEmitter(_module, _language);

			if (_monoAssembly != null)
			{
				_monoModule = AssemblyDef.Load(_monoAssembly).Modules.Single();
				_monoEmitter = new TranslationEmitter(_monoModule, _language);
			}
		}

		private void ApplyBuildProperties()
		{
			if (!File.Exists(GetPath("Info.json")))
			{
				return;
			}

			var info = JsonConvert.DeserializeObject<BuildProperties>(File.ReadAllText(GetPath("Info.json")));
			var data = info.ToBytes();

			_mod.AddFile("Info", data);
		}

		private void ApplyTmodProperties()
		{
			if (!File.Exists(GetPath("ModInfo.json")))
			{
				return;
			}

			var prop = JsonConvert.DeserializeObject<TmodProperties>(File.ReadAllText(GetPath("ModInfo.json")));
			_mod.Properties = prop;
		}

		private void ApplyItems()
		{
			var texts = LoadTranslations<ItemTranslation>("Items");

			foreach (var text in texts)
			{
				ApplyItemsInternal(text, _emitter);
				ApplyItemsInternal(text, _monoEmitter);
			}

			void ApplyItemsInternal(ItemTranslation translation, TranslationEmitter emitter)
			{
				if (translation == null || emitter == null)
					return;

				var fullName = string.Concat(translation.Namespace, ".", translation.TypeName);
				var type = emitter.Module.Find(fullName, true);
				if (type == null)
					return;

				var method = type.FindMethod("SetStaticDefaults", MethodSig.CreateInstance(_module.CorLibTypes.Void));
				if (method?.HasBody == true)
				{
					if (!string.IsNullOrEmpty(translation.Name))
						emitter.Emit(method, "DisplayName", translation.Name);
					if (!string.IsNullOrEmpty(translation.ToolTip))
						emitter.Emit(method, "Tooltip", translation.ToolTip);
				}

				method = type.FindMethod("ModifyTooltips");
				if (method?.HasBody == true)
				{
					var inst = method.Body.Instructions;
					var listIndex = 0;

					for (var index = 0; index < inst.Count; index++)
					{
						var ins = inst[index];

						if (ins.OpCode != OpCodes.Newobj || !(ins.Operand is MemberRef m) || !m.DeclaringType.Name.Equals("TooltipLine"))
							continue;

						ins = inst[index - 1];

						if (ins.OpCode.Equals(OpCodes.Ldstr) && inst[index - 2].OpCode.Equals(OpCodes.Ldstr))
						{
							inst[index - 2].Operand = translation.ModifyTooltips[listIndex++];
							inst[index - 1].Operand = translation.ModifyTooltips[listIndex++];
						}
						else if (ins.OpCode.Equals(OpCodes.Call) && ins.Operand is MemberRef n && n.Name.Equals("Concat")) // for thorium mod
						{
							int index2 = index, reversedListIndex = 0, argumentCount = 0;
							var total = n.MethodSig.Params.Count + 1;

							var reversedList = translation.ModifyTooltips.GetRange(index, translation.ModifyTooltips.Count - index);
							reversedList.Reverse();

							var ldstrList = new List<Instruction>();
							while (--index2 > 0 && argumentCount < total)
							{
								ins = inst[index2];
								if (ins.OpCode.Equals(OpCodes.Ldelem_Ref))
								{
									argumentCount++;
								}
								else if (ins.OpCode.Equals(OpCodes.Ldstr))
								{
									argumentCount++;
									ldstrList.Add(ins);
								}
							}

							foreach (var ldstrInstruction in ldstrList)
							{
								ldstrInstruction.Operand = reversedList[reversedListIndex++];
								index++;
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

						if ((ins = inst[++index]).OpCode == OpCodes.Stfld && ins.Operand is MemberRef m)
						{
							switch (m.Name)
							{
								case "setBonus":
									inst[index - 1].Operand = translation.SetBonus;
									break;
							}
						}


					}
				}
			}
		}

		private void ApplyNpcs()
		{
			var texts = LoadTranslations<NpcTranslation>("Npcs");

			foreach (var text in texts)
			{
				ApplyNpcsInternal(text, _emitter);
				ApplyNpcsInternal(text, _monoEmitter);
			}

			void ApplyNpcsInternal(NpcTranslation translation, TranslationEmitter emitter)
			{
				if (translation == null || emitter == null)
					return;

				var fullName = string.Concat(translation.Namespace, ".", translation.TypeName);
				var type = emitter.Module.Find(fullName, true);
				if (type == null)
					return;

				var method = type.FindMethod("SetStaticDefaults", MethodSig.CreateInstance(_module.CorLibTypes.Void));
				if (method?.HasBody == true)
				{
					if (!string.IsNullOrEmpty(translation.Name))
						emitter.Emit(method, "DisplayName", translation.Name);
				}

				method = type.FindMethod("GetChat");
				if (method?.HasBody == true)
				{
					var inst = method.Body.Instructions;

					var listindex = 0;

					for (var index = 0; index < inst.Count; index++)
					{
						var ins = inst[index];

						if (ins.OpCode != OpCodes.Ldstr)
							continue;

						ins = inst[++index];

						if ((ins.OpCode.Equals(OpCodes.Call) || ins.OpCode.Equals(OpCodes.Callvirt))
							&& ins.Operand is IMethodDefOrRef m)
							if (!m.Name.ToString().Equals("Concat") || m.MethodSig.Params.Count == 1)
								continue;

						inst[index - 1].Operand = translation.ChatTexts[listindex++];
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

						ins = inst[index - 1];

						if (ins.OpCode.Equals(OpCodes.Ldarg_1))
							inst[index].Operand = translation.ShopButton1;
						else if (ins.OpCode.Equals(OpCodes.Ldarg_2))
							inst[index].Operand = translation.ShopButton2;
					}
				}
			}
		}

		private void ApplyBuffs()
		{
			var texts = LoadTranslations<BuffTranslation>("Buffs");

			foreach (var text in texts)
			{
				ApplyBuffsInternal(text, _emitter);
				ApplyBuffsInternal(text, _monoEmitter);
			}

			void ApplyBuffsInternal(BuffTranslation translation, TranslationEmitter emitter)
			{
				if (translation == null || emitter == null)
					return;

				var fullName = string.Concat(translation.Namespace, ".", translation.TypeName);
				var type = emitter.Module.Find(fullName, true);
				if (type == null)
					return;

				var method = type.FindMethod("SetDefaults", MethodSig.CreateInstance(_module.CorLibTypes.Void));
				if (method?.HasBody == true)
				{
					if (!string.IsNullOrEmpty(translation.Name))
						emitter.Emit(method, "DisplayName", translation.Name);
					if (!string.IsNullOrEmpty(translation.Tip))
						emitter.Emit(method, "Description", translation.Tip);
				}
			}
		}

		private void ApplyMiscs()
		{
			var texts = LoadTranslations<NewTextTranslation>("Miscs");

			foreach (var text in texts)
			{
				ApplyMiscsInternal(text, _emitter);
				ApplyMiscsInternal(text, _monoEmitter);
			}

			void ApplyMiscsInternal(NewTextTranslation translation, TranslationEmitter emitter)
			{
				if (translation == null || emitter == null)
					return;

				var fullName = string.Concat(translation.Namespace, ".", translation.TypeName);
				var type = emitter.Module.Find(fullName, true);

				var method = type?.FindMethod(translation.Method);

				if (method?.HasBody != true)
					return;

				var inst = method.Body.Instructions;
				var listIndex = 0;

				for (var index = 0; index < inst.Count; index++)
				{
					var ins = inst[index];

					if (!ins.OpCode.Equals(OpCodes.Call) || !(ins.Operand is IMethodDefOrRef m) ||
						!m.Name.ToString().Equals("NewText", StringComparison.Ordinal))
						continue;

					if ((ins = inst[index - 5]).OpCode.Equals(OpCodes.Ldstr))
					{
						ins.Operand = translation.Contents[listIndex++];
					}
					else if (ins.OpCode.Equals(OpCodes.Call) &&
							 ins.Operand is IMethodDefOrRef n &&
							 n.Name.ToString().Equals("Concat", StringComparison.Ordinal))
					{
						var index2 = index;
						var count = 0;
						var total = n.MethodSig.Params.Count;
						var list = translation.Contents.GetRange(listIndex, translation.Contents.Count - listIndex).AsEnumerable().Reverse().ToList();
						var currentIndex = 0;
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
								listIndex++;
								ins.Operand = list[currentIndex++];
							}
						}
					}
				}
			}
		}

		private void ApplyMapEntries()
		{
			var texts = LoadTranslations<MapEntryTranslation>("Tiles");

			foreach (var text in texts)
			{
				ApplyMapEntriesInternal(text, _emitter);
				ApplyMapEntriesInternal(text, _monoEmitter);
			}

			void ApplyMapEntriesInternal(MapEntryTranslation translation, TranslationEmitter emitter)
			{
				if (translation == null || emitter == null)
					return;

				var fullName = string.Concat(translation.Namespace, ".", translation.TypeName);
				var type = emitter.Module.Find(fullName, true);

				var method = type.FindMethod("SetDefaults", MethodSig.CreateInstance(_module.CorLibTypes.Void));
				if (method?.HasBody != true)
					return;

				var inst = method.Body.Instructions;

				for (var index = 0; index < inst.Count; index++)
				{
					var ins = inst[index];

					if (ins.OpCode != OpCodes.Call)
						continue;

					if (ins.Operand is IMethodDefOrRef m &&
						string.Equals(m.Name.ToString(), "CreateMapEntryName") &&
						string.Equals(m.DeclaringType.Name, "ModTile", StringComparison.Ordinal))
					{
						ins = inst[++index];
						if (!ins.IsStloc())
						{
							continue;
						}

						var local = ins.GetLocal(method.Body.Variables);
						emitter.Emit(method, local, translation.Name);
					}
				}
			}
		}

		private void ApplyCustomTranslations()
		{
			var texts = LoadTranslations<CustomTranslation>("Customs");

			ApplyCustomTranslationsInternal(texts, _emitter);
			ApplyCustomTranslationsInternal(texts, _monoEmitter);

			void ApplyCustomTranslationsInternal(IList<CustomTranslation> translations, TranslationEmitter emitter)
			{
				foreach (var type in emitter.Module.Types)
				{
					foreach (var method in type.Methods)
					{
						if (!method.HasBody)
							continue;

						var inst = method.Body.Instructions;
						for (var index = 0; index < inst.Count; index++)
						{
							var ins = inst[index];

							if (!ins.OpCode.Equals(OpCodes.Call) ||
							    !(ins.Operand is IMethod m) ||
							    !string.Equals(m.Name.ToString(), "CreateTranslation"))
								continue;

							if (!(ins = inst[++index]).IsStloc())
								continue;

							var key = inst[index - 2].Operand as string;
							var content = translations.FirstOrDefault(x => string.Equals(key, x.Key, StringComparison.Ordinal))?.Value;

							if (string.IsNullOrEmpty(content))
								continue;

							var local = ins.GetLocal(method.Body.Variables);

							emitter.Emit(method, local, content, index + 1);
						}
					}
				}
			}
		}

		private void Save()
		{
			var tmp = Path.GetTempFileName();
			_module.Write(tmp);
			_mod.WriteMainAssembly(File.ReadAllBytes(tmp), false);

			if (_monoAssembly != null)
			{
				tmp = Path.GetTempFileName();
				_monoModule.Write(tmp);
				_mod.WriteMainAssembly(File.ReadAllBytes(tmp), true);
			}

			_mod.Save(_mod.Name + "_patched.tmod");
#if DEBUG
			_module.Write("test-mod.dll");
			_monoModule?.Write("test-mod-mono.dll");
#endif
		}

		private IList<T> LoadTranslations<T>(string category) where T : ITranslation
		{
			var texts = new List<T>();

			foreach (var file in Directory.EnumerateFiles(GetPath(category), "*.json"))
			{
				using (var sr = new StreamReader(File.OpenRead(file)))
				{
					var list = JsonConvert.DeserializeObject<List<T>>(sr.ReadToEnd());
					if (list != null)
						texts.AddRange(list);
				}
			}

			return texts;
		}

		private string GetPath(params string[] paths) => Path.Combine(_contentPath, Path.Combine(paths));
	}
}
