using dnlib.DotNet;

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
    }
}
