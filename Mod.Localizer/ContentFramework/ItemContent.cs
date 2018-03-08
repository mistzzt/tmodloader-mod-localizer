using System.Collections.Generic;
using dnlib.DotNet;

namespace Mod.Localizer.ContentFramework
{
    public sealed class ItemContent : Content
    {
        public string Name { get; set; } = string.Empty;

        public string ToolTip { get; set; } = string.Empty;

        public string SetBonus { get; set; } = string.Empty;

        public IList<string> ModifyTooltips { get; set; } = new List<string>();

        public ItemContent(TypeDef type) : base(type) { }

        public ItemContent() { }
    }
}
