using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using ModLocalizer.Extensions;

namespace ModLocalizer
{
    internal sealed class TranslationEmitter
    {
        public ModuleDef Module { get; }

        /// <summary>
        /// Initializes a new instance of TranslationEmitter.
        /// </summary>
        /// <param name="module">The module of the target assembly.</param>
        /// <param name="gameCulture">The game culture selection.</param>
        /// <param name="modName">The mod name.</param>
        public TranslationEmitter(ModuleDef module, string gameCulture, string modName)
        {
            _modName = modName;
            if (gameCulture == null) throw new ArgumentNullException(nameof(gameCulture));

            Module = module ?? throw new ArgumentNullException(nameof(module));

            var importer = new Importer(Module);

            var terraria = module.GetAssemblyRef("Terraria") ?? new AssemblyRefUser("Terraria", DefaultConfigurations.TerrariaVersion);
            _modTranslationType = new TypeRefUser(Module, "Terraria.ModLoader", "ModTranslation", terraria);

            _modTranslationSetDefaultMethod = new MemberRefUser(Module, "SetDefault",
                MethodSig.CreateInstance(Module.CorLibTypes.Void, Module.CorLibTypes.String),
                _modTranslationType);

            var gameCultureType = new TypeRefUser(Module, "Terraria.Localization", "GameCulture", terraria);
            _modTranslationAddTranslationMethod = new MemberRefUser(Module, "AddTranslation",
                MethodSig.CreateInstance(Module.CorLibTypes.Void, new ClassSig(gameCultureType), Module.CorLibTypes.String),
                _modTranslationType);

            _gameCultureField = new MemberRefUser(Module, gameCulture,
                new FieldSig(new ClassSig(gameCultureType)),
                gameCultureType);

            var languageType = new TypeRefUser(Module, "Terraria.Localization", "Language", terraria);
            _getTextValueMethod = new MemberRefUser(Module, "GetTextValue", MethodSig.CreateStatic(Module.CorLibTypes.String, Module.CorLibTypes.String), languageType);

            var modType = new TypeRefUser(Module, "Terraria.ModLoader", "Mod", terraria);

            _modAddTranslationMethod = new MemberRefUser(Module, "AddTranslation", MethodSig.CreateInstance(Module.CorLibTypes.Void, new ClassSig(_modTranslationType)), modType);
            _modCreateTranslationMethod = new MemberRefUser(Module, "CreateTranslation", MethodSig.CreateInstance(new ClassSig(_modTranslationType), Module.CorLibTypes.String), modType);

            var type = Module.Types.Single(x => string.Equals(x.BaseType?.FullName, "Terraria.ModLoader.Mod",
                StringComparison.Ordinal));
            var ctor = importer.Import(typeof(CompilerGeneratedAttribute).GetConstructor(new Type[0])) as IMethodDefOrRef;

            _modSetTranslationMethod = new MethodDefUser(ModSetTranslationMethod, MethodSig.CreateInstance(Module.CorLibTypes.Void), MethodAttributes.Private);
            type.Methods.Add(_modSetTranslationMethod);

            _modSetTranslationMethod.CustomAttributes.Add(new CustomAttribute(ctor));
            _modSetTranslationMethod.Body = new CilBody
            {
                Instructions = { OpCodes.Ret.ToInstruction() },
                Variables = { new Local(new ClassSig(_modTranslationType), "translation", 0) }
            };

            var loadMethod = type.FindMethod("Load", MethodSig.CreateInstance(Module.CorLibTypes.Void));
            if (loadMethod?.HasBody != true)
            {
                Console.WriteLine("Could not find Mod.Load() method; trying to add one.");

                loadMethod = new MethodDefUser("Load", MethodSig.CreateInstance(Module.CorLibTypes.Void), MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual)
                {
                    Body = new CilBody
                    {
                        Instructions = { OpCodes.Ret.ToInstruction() },
                    }
                };

                type.Methods.Add(loadMethod);
            }

            loadMethod.Body.Instructions.AppendLast(new[]
            {
                OpCodes.Ldarg_0.ToInstruction(),
                OpCodes.Call.ToInstruction(_modSetTranslationMethod),
                OpCodes.Ret.ToInstruction()
            });
        }

        /// <summary>
        /// Emits codes to add translations for existing ModTranslation instances.
        /// </summary>
        /// <param name="method">The target method.</param>
        /// <param name="propertyName">The property name of ModTranslation instance.</param>
        /// <param name="content">The translation content.</param>
        public void Emit(MethodDef method, string propertyName, string content)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (method.Module != Module) throw new ArgumentOutOfRangeException(nameof(method));

            var instructions = method.Body.Instructions;

            var translationPropertyGetter = new MemberRefUser(Module, "get_" + propertyName,
                MethodSig.CreateInstance(new ClassSig(_modTranslationType)),
                method.DeclaringType.BaseType);

            instructions.AppendLast(new[]
            {
                OpCodes.Ldarg_0.ToInstruction(),
                OpCodes.Call.ToInstruction(translationPropertyGetter),
                OpCodes.Ldsfld.ToInstruction(_gameCultureField),
                OpCodes.Ldstr.ToInstruction(content),
                OpCodes.Callvirt.ToInstruction(_modTranslationAddTranslationMethod)
            });

            method.Body.SimplifyBranches();
            method.Body.OptimizeBranches();
        }

        /// <summary>
        /// Emits codes to add translations for a locally defined ModTranslation instance.
        /// </summary>
        /// <param name="method">The target method.</param>
        /// <param name="local">The local which stores ModTranslation instance.</param>
        /// <param name="content">The translation content.</param>
        /// <param name="line">The index of instruction to be inserted before.</param>
        public void Emit(MethodDef method, Local local, string content, int line)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));
            if (local == null) throw new ArgumentNullException(nameof(local));
            if (method.Module != Module) throw new ArgumentOutOfRangeException(nameof(method));

            var instructions = method.Body.Instructions;
            instructions.Insert(line, new[]
            {
                OpCodes.Ldloc_S.ToInstruction(local),
                OpCodes.Ldsfld.ToInstruction(_gameCultureField),
                OpCodes.Ldstr.ToInstruction(content),
                OpCodes.Callvirt.ToInstruction(_modTranslationAddTranslationMethod)
            });

            method.Body.SimplifyBranches();
            method.Body.OptimizeBranches();
        }

        /// <summary>
        /// Emits codes to add translations for a locally defined ModTranslation instance.
        /// </summary>
        /// <param name="method">The target method.</param>
        /// <param name="local">The local which stores ModTranslation instance.</param>
        /// <param name="content">The translation content.</param>
        public void Emit(MethodDef method, Local local, string content)
        {
            Emit(method, local, content, method.Body.Instructions.Count - 1);
        }

        /// <summary>
        /// Emits codes to replace originally `ldstr` instruction with getting translation
        /// from a ModTranslation instance created by localizer.
        /// </summary>
        /// <param name="method">The target method.</param>
        /// <param name="ldstr">The ldstr instruction to be replaced.</param>
        /// <param name="content">The translation content.</param>
        public void Emit(MethodDef method, Instruction ldstr, string content)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));
            if (ldstr == null) throw new ArgumentNullException(nameof(ldstr));
            if (method.Module != Module) throw new ArgumentOutOfRangeException(nameof(method));

            var index = method.Body.Instructions.IndexOf(ldstr);
            if (index == -1) throw new ArgumentOutOfRangeException(nameof(ldstr));

            var key = AddTranslation(GetTranslationKey(method.DeclaringType.Name), (string)ldstr.Operand, content);
            ldstr.Operand = key;

            method.Body.Instructions.Insert(index + 1, OpCodes.Call.ToInstruction(_getTextValueMethod));

            method.Body.SimplifyBranches();
            method.Body.OptimizeBranches();
        }

        private string AddTranslation(string key, string @default, string content)
        {
            var instructions = _modSetTranslationMethod.Body.Instructions;

            instructions.AppendLast(new[]
            {
                OpCodes.Ldarg_0.ToInstruction(),
                OpCodes.Ldstr.ToInstruction(key),
                OpCodes.Call.ToInstruction(_modCreateTranslationMethod),
                OpCodes.Stloc_0.ToInstruction(),

                OpCodes.Ldloc_0.ToInstruction(),
                OpCodes.Ldstr.ToInstruction(@default),
                OpCodes.Callvirt.ToInstruction(_modTranslationSetDefaultMethod),

                OpCodes.Ldloc_0.ToInstruction(),
                OpCodes.Ldsfld.ToInstruction(_gameCultureField),
                OpCodes.Ldstr.ToInstruction(content),
                OpCodes.Callvirt.ToInstruction(_modTranslationAddTranslationMethod),

                OpCodes.Ldarg_0.ToInstruction(),
                OpCodes.Ldloc_0.ToInstruction(),
                OpCodes.Call.ToInstruction(_modAddTranslationMethod)
            });

            // translation implementation in tModLoader:
            // Mods.<ModName>.<Key>
            return string.Format("Mods.{0}.{1}", _modName, key);
        }

        private string GetTranslationKey(string typeName) => $"LCR.{typeName}.{GetIndex(typeName)}";
        private readonly IDictionary<string, int> _index = new Dictionary<string, int>();

        private int GetIndex(string typeName)
        {
            if (!_index.ContainsKey(typeName))
            {
                _index.Add(typeName, default(int));
            }

            return _index[typeName]++;
        }

        private readonly TypeRefUser _modTranslationType;

        private readonly MemberRefUser _modTranslationAddTranslationMethod, _modTranslationSetDefaultMethod;
        private readonly MemberRefUser _gameCultureField, _getTextValueMethod;
        private readonly MemberRefUser _modAddTranslationMethod, _modCreateTranslationMethod;

        private readonly MethodDef _modSetTranslationMethod;

        private readonly string _modName;

        private const string ModSetTranslationMethod = "Localizer_setTranslation<>";
    }
}
