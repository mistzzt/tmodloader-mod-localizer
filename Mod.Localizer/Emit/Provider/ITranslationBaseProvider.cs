using dnlib.DotNet;

namespace Mod.Localizer.Emit.Provider
{
    public interface ITranslationBaseProvider
    {
        MemberRef GameCultureField { get; }

        MemberRef AddTranslationMethod { get; }

        MemberRef GetTextValueMethod { get; }

        TypeRef ModTranslationType { get; }

        string CreateTranslation(string source, string value);

        void AddTranslation(string key, string value);
    }
}
