using dnlib.DotNet;

namespace Mod.Localizer.ContentFramework
{
    public sealed class BuffContent : Content
    {
        public BuffContent(TypeDef type) : base(type)
        {
        }

        public string Name { get; set; } = string.Empty;

        public string Tip { get; set; } = string.Empty;
    }
}
