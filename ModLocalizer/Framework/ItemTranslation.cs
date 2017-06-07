using System.Collections.Generic;

namespace ModLocalizer.Framework
{
	public sealed class ItemTranslation : ITranslation
	{
		public string TypeName { get; set; } = string.Empty;

		public string Namespace { get; set; } = string.Empty;

		public string Name { get; set; } = string.Empty;

		public string ToolTip { get; set; } = string.Empty;

		public List<string> ModifyTooltips { get; set; } = new List<string>();
	}
}
