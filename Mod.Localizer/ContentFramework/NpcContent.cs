using System.Collections.Generic;
using dnlib.DotNet;

namespace Mod.Localizer.ContentFramework
{
    public sealed class NpcContent : Content
    {
        public string Name { get; set; } = string.Empty;

        public string ShopButton1 { get; set; } = string.Empty;

        public string ShopButton2 { get; set; } = string.Empty;

        public IList<string> ChatTexts { get; set; } = new List<string>();

        public IList<string> TownNpcNames { get; set; } = new List<string>();
        
        public NpcContent(TypeDef type) : base(type) { }

        public NpcContent() { }
    }
}
