namespace ModLocalizer.Framework
{
	public sealed class BuffTranslation : ITranslation
	{
		public string TypeName { get; set; } = string.Empty;

		public string Namespace { get; set; } = string.Empty;

		public string Name { get; set; } = string.Empty;

		public string Tip { get; set; } = string.Empty;
	}
}
