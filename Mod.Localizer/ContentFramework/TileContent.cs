﻿using dnlib.DotNet;

namespace Mod.Localizer.ContentFramework
{
    public sealed class TileContent : Content
    {
        public TileContent(TypeDef type) : base(type)
        {
        }

        public string Name { get; set; } = string.Empty;
    }
}
