using System.Collections.Generic;
using dnlib.DotNet;

namespace Mod.Localizer.ContentFramework
{
    public sealed class MiscContent : Content
    {
        public MiscContent(IMemberDef method) : base(method.DeclaringType)
        {
            Method = method.Name;
        }
       
        public IList<string> Contents { get; set; } = new List<string>();

        /// <summary>
        /// Stores the source method. It should not be modified.
        /// </summary>
        public string Method { get; set; }
    }
}
