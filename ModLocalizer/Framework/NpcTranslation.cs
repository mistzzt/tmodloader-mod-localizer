using System.Collections.Generic;

namespace ModLocalizer.Framework
{
	public sealed class NpcTranslation : ITranslation
	{
		public string TypeName { get; set; } = string.Empty;

		public string Namespace { get; set; } = string.Empty;

		public string Name { get; set; } = string.Empty;

		public string ShopButton1 { get; set; } = string.Empty;

		public string ShopButton2 { get; set; } = string.Empty;

		public List<string> ChatTexts { get; set; } = new List<string>();
	}
}
