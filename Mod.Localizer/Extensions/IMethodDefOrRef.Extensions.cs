using dnlib.DotNet;

namespace Mod.Localizer.Extensions
{
    internal static class MethodDefOrRefExtensions
    {
        public static bool IsMethod(this IMethodDefOrRef defOrRef, string type, string method)
        {
            return string.Equals(defOrRef.DeclaringType.Name, type) &&
                   string.Equals(defOrRef.Name, method);
        }
    }
}
