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
		private readonly byte[] _assembly;
		private readonly byte[] _monoAssembly;

		private ModuleDef _module;
		private ModuleDef _monoModule;

		private TranslationEmitter _emitter;
		private TranslationEmitter _monoEmitter;

		public Patcher(TmodFile mod, string contentPath)
		{
			_mod = mod;
			_contentPath = contentPath;

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

			Save();
		}

		private void LoadAssemblies()
		{
			_module = AssemblyDef.Load(_assembly).Modules.Single();
			_emitter = new TranslationEmitter(_module, "Chinese");

			if (_monoAssembly != null)
			{
				_monoModule = AssemblyDef.Load(_monoAssembly).Modules.Single();
				_monoEmitter = new TranslationEmitter(_monoModule, "Chinese");
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
				if(!string.IsNullOrEmpty(translation.Name))
					emitter.Emit(method, "DisplayName", translation.Name);
				if(!string.IsNullOrEmpty(translation.ToolTip))
					emitter.Emit(method, "Tooltip", translation.ToolTip);

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
							inst[index - 2].Operand = translation.ModifyTooltips[0];
							inst[index - 1].Operand = translation.ModifyTooltips[1];
						}
						else if (ins.OpCode.Equals(OpCodes.Call) && ins.Operand is MemberRef n && n.Name.Equals("Concat"))
						{
							var index2 = index;
							var count = 0;
							var listIndex = 0;
							var total = n.MethodSig.Params.Count + 1;
							var list = translation.ModifyTooltips.AsEnumerable().Reverse().ToList();
							while (--index2 > 0 && count < total)
							{
								ins = inst[index2];
								if (ins.OpCode.Equals(OpCodes.Ldelem_Ref))
								{
									count++;
								}
								else if (ins.OpCode.Equals(OpCodes.Ldstr))
								{
									ins.Operand = list[listIndex++];
									count++;
								}
							}
						}


					}
				}
			}
		}

		private void ApplyNpcs()
		{

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
