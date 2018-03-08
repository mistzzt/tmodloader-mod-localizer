using dnlib.DotNet;
using Newtonsoft.Json;

namespace Mod.Localizer.ContentFramework
{
    public abstract class Content
    {
        public string TypeName { get; set; }

        public string Namespace { get; set; }

        protected Content(TypeDef type)
        {
            TypeName = type.Name;
            Namespace = type.Namespace;
        }

        protected Content() { }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
}
