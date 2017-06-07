using System;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using ModLocalizer.Extensions;

namespace ModLocalizer
{
	internal sealed class TranslationEmitter
	{
		public ModuleDef Module { get; }

		public TranslationEmitter(ModuleDef module, string gameCulture)
		{
			if (gameCulture == null) throw new ArgumentNullException(nameof(gameCulture));

			Module = module ?? throw new ArgumentNullException(nameof(module));

			var gameCultureType = new TypeRefUser(Module, "Terraria.Localization", "GameCulture", _terraria);
			_modTranslation = new TypeRefUser(Module, "Terraria.ModLoader", "ModTranslation", _terraria);
			_addTranslation = new MemberRefUser(Module, "AddTranslation",
				MethodSig.CreateInstance(Module.CorLibTypes.Void, new ClassSig(gameCultureType), Module.CorLibTypes.String),
				_modTranslation);
			_gameCultureField = new MemberRefUser(Module, gameCulture,
				new FieldSig(new ClassSig(gameCultureType)),
				gameCultureType);
		}

		public void Emit(MethodDef method, string propertyName, string content)
		{
			if (method == null) throw new ArgumentNullException(nameof(method));
			if (content == null) throw new ArgumentNullException(nameof(content));
			if (method.Module != Module) throw new ArgumentOutOfRangeException(nameof(method));

			var instructions = method.Body.Instructions;

			var translationPropertyGetter = new MemberRefUser(Module, "get_" + propertyName,
				MethodSig.CreateInstance(new ClassSig(_modTranslation)),
				method.DeclaringType.BaseType);

			instructions.Insert(instructions.Count - 1, new[]
			{
				OpCodes.Ldarg_0.ToInstruction(),
				OpCodes.Call.ToInstruction(translationPropertyGetter),
				OpCodes.Ldsfld.ToInstruction(_gameCultureField),
				OpCodes.Ldstr.ToInstruction(content),
				OpCodes.Callvirt.ToInstruction(_addTranslation)
			});
		}

		private readonly AssemblyRefUser _terraria = new AssemblyRefUser("Terraria", new Version(1, 3, 5, 1));
		private readonly TypeRefUser _modTranslation;
		private readonly MemberRefUser _addTranslation, _gameCultureField;
	}
}
