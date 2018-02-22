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
    internal sealed class HardCodedTranslationProvider : ITranslationBaseProvider
    {
        protected readonly ModuleDef Module;
        protected readonly GameCultures Lang;

        protected readonly Logger Logger;

        private readonly IDictionary<string, int> _index = new Dictionary<string, int>();

        protected HardCodedTranslationProvider(ModuleDef module, GameCultures lang)
        {
            Module = module ?? throw new ArgumentNullException(nameof(module));
            Lang = lang;

            Logger = LogManager.GetLogger(GetType().Name);

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

        protected MemberRefUser GetTextValueMethod { get; private set; }

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
            TerrariaAssembly = Module.GetAssemblyRef(assemblyName) ?? new AssemblyRefUser(assemblyName);

            ModTranslationType = new TypeRefUser(Module,
                typeof(Terraria.ModLoader.ModTranslation).Namespace,
                nameof(Terraria.ModLoader.ModTranslation), TerrariaAssembly);

            SetDefaultMethod = new MemberRefUser(Module,
                nameof(Terraria.ModLoader.ModTranslation.SetDefault),
                MethodSig.CreateInstance(Module.CorLibTypes.Void, Module.CorLibTypes.String),
                ModTranslationType);

            var gameCultureType = new TypeRefUser(Module,
                typeof(Terraria.Localization.GameCulture).Namespace,
                nameof(Terraria.Localization.GameCulture), TerrariaAssembly);
            AddTranslationMethod = new MemberRefUser(Module,
                nameof(Terraria.ModLoader.ModTranslation.AddTranslation),
                MethodSig.CreateInstance(Module.CorLibTypes.Void, new ClassSig(gameCultureType), Module.CorLibTypes.String));

            GameCultureField = new MemberRefUser(Module, Lang.ToString(),
                new FieldSig(new ClassSig(gameCultureType)), gameCultureType);

            GetTextValueMethod = new MemberRefUser(Module,
                nameof(Terraria.Localization.Language.GetTextValue),
                MethodSig.CreateStatic(Module.CorLibTypes.String, Module.CorLibTypes.String, Module.CorLibTypes.String),
                new TypeRefUser(Module,
                    typeof(Terraria.Localization.Language).Namespace,
                    nameof(Terraria.Localization.Language), TerrariaAssembly));

            var modType = new TypeRefUser(Module,
                typeof(Terraria.ModLoader.Mod).Namespace,
                nameof(Terraria.ModLoader.Mod), TerrariaAssembly);

            AddGlobalTranslationMethod = new MemberRefUser(Module,
                nameof(Terraria.ModLoader.Mod.AddTranslation),
                MethodSig.CreateInstance(Module.CorLibTypes.Void, new ClassSig(ModTranslationType)),
                modType);

            CreateGlobalTranslationMethod = new MemberRefUser(Module,
                nameof(Terraria.ModLoader.Mod.CreateTranslation),
                MethodSig.CreateInstance(new ClassSig(ModTranslationType), Module.CorLibTypes.String),
                modType);
        }

        private void CreateInitializationMethod()
        {
            // load the compiler generated attribute
            var ctor = new Importer(Module).Import(
                typeof(CompilerGeneratedAttribute).GetConstructor(new Type[0])
            ) as IMethodDefOrRef;

            // create the method
            InitializeTranslationMethod = new MethodDefUser(
                InitializeTranslationMethodName,
                MethodSig.CreateInstance(Module.CorLibTypes.Void),
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
            var modLoadMethod = Module.Types.Single(
                x => x.HasBaseType(typeof(Terraria.ModLoader.Mod).FullName)
            ).FindMethod(nameof(Terraria.ModLoader.Mod.Load), MethodSig.CreateInstance(Module.CorLibTypes.Void));

            if (modLoadMethod?.HasBody != true)
            {
                Logger.Info("Could not find Mod.Load(), create one instead.");

                modLoadMethod = new MethodDefUser(
                    nameof(Terraria.ModLoader.Mod.Load),
                    MethodSig.CreateInstance(Module.CorLibTypes.Void),
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

            var index = _index[source]++;
            var key = string.Format(TranslationKeyFormat, source, index);

            InitializeTranslationMethod.Body.AppendLast(new[]
            {
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
            throw new NotImplementedException();
        }
    }
}
