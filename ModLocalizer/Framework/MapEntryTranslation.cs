namespace ModLocalizer.Framework
{
	public sealed class MapEntryTranslation : ITranslation
	{
		public string TypeName { get; set; } = string.Empty;

		public string Namespace { get; set; } = string.Empty;

		public string Name { get; set; } = string.Empty;
	}
}
