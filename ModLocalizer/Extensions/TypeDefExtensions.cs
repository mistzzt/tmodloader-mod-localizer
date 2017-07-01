using System;
using dnlib.DotNet;

namespace ModLocalizer.Extensions
{
	internal static class TypeDefExtensions
	{
		public static bool HasBaseType(this ITypeDefOrRef type, string fullName)
		{
			if (type == null) throw new ArgumentNullException(nameof(type));
			if (fullName == null) throw new ArgumentNullException(nameof(fullName));

			while ((type = type.GetBaseType()) != null &&
				   !string.Equals(type.FullName, fullName, StringComparison.Ordinal)) { }

			return string.Equals(type?.FullName, fullName, StringComparison.Ordinal);
		}
	}
}
