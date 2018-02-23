using System;
using Mod.Localizer.Resources;

namespace Mod.Localizer
{
    public sealed class ProcessTargetAttribute : Attribute
    {
        public string Method { get; }
        public string[] Value { get; }

        public ProcessTargetAttribute(string method, params string[] value)
        {
            if (string.IsNullOrWhiteSpace(method))
                throw new ArgumentException(Strings.ProcessTargetNullCheck, nameof(method));

            Method = method;
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }
    }
}
