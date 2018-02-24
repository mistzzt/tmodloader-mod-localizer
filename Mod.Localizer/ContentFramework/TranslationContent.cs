using dnlib.DotNet;

namespace Mod.Localizer.ContentFramework
{
    public sealed class TranslationContent : Content
    {
        public string Key { get; set; }
        
        public string Value { get; set; }

        public TranslationContent(TypeDef type) : base(type)
        {
        }
    }
}
