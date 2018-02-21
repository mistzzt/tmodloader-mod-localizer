using dnlib.DotNet;
using Newtonsoft.Json;

namespace Mod.Localizer.ContentFramework
{
    public abstract class Content
    {
        public string TypeName { get; }

        public string Namespace { get; }

        protected Content(TypeDef type)
        {
            TypeName = type.Name;
            Namespace = type.Namespace;
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
}
