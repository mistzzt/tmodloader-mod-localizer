using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Mod.Localizer.Extensions;
using NLog;

namespace Mod.Localizer.Emit.Provider
{
    public sealed class HardCodedTranslationProvider : ITranslationBaseProvider
    {
        private readonly TmodFileWrapper.ITmodFile _modFile;
        private readonly ModuleDef _module;
        private readonly GameCultures _lang;

        private readonly Logger _logger;

        private readonly IDictionary<string, int> _index = new Dictionary<string, int>();

        public HardCodedTranslationProvider(TmodFileWrapper.ITmodFile modFile, ModuleDef module, GameCultures lang)
        {
            _modFile = modFile;
            _module = module ?? throw new ArgumentNullException(nameof(module));
            _lang = lang;

            _logger = LogManager.GetLogger(GetType().Name);

            LoadTypeDefinitions();
            CreateInitializationMethod();
        }

        public TypeRef ModTranslationType { get; private set; }

        /// <summary>
        /// Method <code>ModTranslation.AddTranslation</code>
        /// </summary>
        public MemberRef AddTranslationMethod { get; private set; }

        protected MemberRefUser SetDefaultMethod { get; private set; }

        public MemberRef GameCultureField { get; private set; }

        public MemberRef GetTextValueMethod { get; private set; }

        protected MethodDef InitializeTranslationMethod { get; private set; }

        /// <summary>
        /// Method <code>Mod.AddTranslation</code>
        /// </summary>
        protected MemberRefUser AddGlobalTranslationMethod { get; private set; }

        protected MemberRefUser CreateGlobalTranslationMethod { get; private set; }

        protected AssemblyRef TerrariaAssembly { get; private set; }

        protected const string InitializeTranslationMethodName = "tModLocalizer_set<$>translation";

        private void LoadTypeDefinitions()
        {
            var assemblyName = typeof(Terraria.BitsByte).Assembly.GetName().Name;
            TerrariaAssembly = _module.GetAssemblyRef(assemblyName) ?? new AssemblyRefUser(assemblyName);

            ModTranslationType = new TypeRefUser(_module,
                typeof(Terraria.ModLoader.ModTranslation).Namespace,
                nameof(Terraria.ModLoader.ModTranslation), TerrariaAssembly);

            SetDefaultMethod = new MemberRefUser(_module,
                nameof(Terraria.ModLoader.ModTranslation.SetDefault),
                MethodSig.CreateInstance(_module.CorLibTypes.Void, _module.CorLibTypes.String),
                ModTranslationType);

            var gameCultureType = new TypeRefUser(_module,
                typeof(Terraria.Localization.GameCulture).Namespace,
                nameof(Terraria.Localization.GameCulture), TerrariaAssembly);
            AddTranslationMethod = new MemberRefUser(_module,
                nameof(Terraria.ModLoader.ModTranslation.AddTranslation),
                MethodSig.CreateInstance(_module.CorLibTypes.Void, new ClassSig(gameCultureType), _module.CorLibTypes.String),
                ModTranslationType);

            GameCultureField = new MemberRefUser(_module, _lang.ToString(),
                new FieldSig(new ClassSig(gameCultureType)), gameCultureType);

            GetTextValueMethod = new MemberRefUser(_module,
                nameof(Terraria.Localization.Language.GetTextValue),
                MethodSig.CreateStatic(_module.CorLibTypes.String, _module.CorLibTypes.String),
                new TypeRefUser(_module,
                    typeof(Terraria.Localization.Language).Namespace,
                    nameof(Terraria.Localization.Language), TerrariaAssembly));

            var modType = new TypeRefUser(_module,
                typeof(Terraria.ModLoader.Mod).Namespace,
                nameof(Terraria.ModLoader.Mod), TerrariaAssembly);

            AddGlobalTranslationMethod = new MemberRefUser(_module,
                nameof(Terraria.ModLoader.Mod.AddTranslation),
                MethodSig.CreateInstance(_module.CorLibTypes.Void, new ClassSig(ModTranslationType)),
                modType);

            CreateGlobalTranslationMethod = new MemberRefUser(_module,
                nameof(Terraria.ModLoader.Mod.CreateTranslation),
                MethodSig.CreateInstance(new ClassSig(ModTranslationType), _module.CorLibTypes.String),
                modType);
        }

        private void CreateInitializationMethod()
        {
            // load the compiler generated attribute
            var ctor = new Importer(_module).Import(
                typeof(CompilerGeneratedAttribute).GetConstructor(new Type[0])
            ) as IMethodDefOrRef;

            // create the method
            InitializeTranslationMethod = new MethodDefUser(
                InitializeTranslationMethodName,
                MethodSig.CreateInstance(_module.CorLibTypes.Void),
                MethodAttributes.Private)
            {
                Body = new CilBody
                {
                    Instructions = { OpCodes.Ret.ToInstruction() },
                    Variables = { new Local(new ClassSig(ModTranslationType)) }
                },
                CustomAttributes =
                {
                    new CustomAttribute(ctor)
                }
            };

            // inject initialization call
            var modType = _module.Types.Single(
                x => x.HasBaseType(typeof(Terraria.ModLoader.Mod).FullName));

            modType.Methods.Add(InitializeTranslationMethod);

            var modLoadMethod = modType
                .FindMethod(nameof(Terraria.ModLoader.Mod.Load), MethodSig.CreateInstance(_module.CorLibTypes.Void));

            if (modLoadMethod?.HasBody != true)
            {
                _logger.Info("Could not find Mod.Load(), create one instead.");

                modLoadMethod = new MethodDefUser(
                    nameof(Terraria.ModLoader.Mod.Load),
                    MethodSig.CreateInstance(_module.CorLibTypes.Void),
                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual
                )
                {
                    Body = new CilBody { Instructions = { OpCodes.Ret.ToInstruction() } }
                };
            }

            modLoadMethod.Body.AppendLast(new[]
            {
                OpCodes.Ldarg_0.ToInstruction(),
                OpCodes.Call.ToInstruction(InitializeTranslationMethod)
            });
        }

        private const string TranslationKeyFormat = "{0}#{1}";

        public string CreateTranslation(string source, string value)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (value == null) throw new ArgumentNullException(nameof(value));

            if (!_index.ContainsKey(source))
            {
                _index.Add(source, default(int));
            }

            // get the key of the new translation instance
            var index = _index[source]++;
            var key = string.Format(TranslationKeyFormat, source, index);

            InitializeTranslationMethod.Body.AppendLast(new[]
            {
                // for navigation when adds translation
                OpCodes.Nop.ToInstruction(),
                
                // create translation with mod name as key
                // to avoid name conflict between mods
                OpCodes.Ldarg_0.ToInstruction(),
                OpCodes.Ldstr.ToInstruction(key),
                OpCodes.Call.ToInstruction(CreateGlobalTranslationMethod),
                OpCodes.Stloc_0.ToInstruction(),

                // set default value
                OpCodes.Ldloc_0.ToInstruction(),
                OpCodes.Ldstr.ToInstruction(value),
                OpCodes.Callvirt.ToInstruction(SetDefaultMethod),

                // add translation
                OpCodes.Ldarg_0.ToInstruction(),
                OpCodes.Ldloc_0.ToInstruction(),
                OpCodes.Call.ToInstruction(AddGlobalTranslationMethod)
            });

            return key;
        }

        public void AddTranslation(string key, string value)
        {
            var instructions = InitializeTranslationMethod.Body.Instructions;

            var target = instructions
                .SingleOrDefault(x => x.OpCode == OpCodes.Ldstr &&
                                      string.Equals(key, (string)x.Operand, StringComparison.Ordinal));

            if (target == null)
            {
                throw new ArgumentOutOfRangeException(nameof(key));
            }

            var nopIndex = InitializeTranslationMethod.Body.Next(
                    instructions.IndexOf(target),
                    x => x.OpCode == OpCodes.Nop);

            // the last instance object in the method
            if (nopIndex == -1)
            {
                nopIndex = instructions.Count - 1;
            }

            instructions.Insert(nopIndex, new[]
            {
                OpCodes.Ldloc_0.ToInstruction(),
                OpCodes.Ldsfld.ToInstruction(GameCultureField),
                OpCodes.Ldstr.ToInstruction(value),

                OpCodes.Call.ToInstruction(AddTranslationMethod)
            });
        }

        public string ToGameLocalizationKey(string modTranslationKey)
        {
            // translation implementation in tModLoader:
            // Mods.<ModName>.<Key>
            return $"Mods.{_modFile.Name}.{modTranslationKey}";
        }
    }
}
