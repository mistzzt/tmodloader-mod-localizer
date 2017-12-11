using System.Collections.Generic;

namespace ModLocalizer.Framework
{
    public sealed class NewTextTranslation : ITranslation
    {
        public string TypeName { get; set; } = string.Empty;

        public string Namespace { get; set; } = string.Empty;

        public string Method { get; set; } = string.Empty;

        public List<string> Contents { get; set; } = new List<string>();
    }
}
