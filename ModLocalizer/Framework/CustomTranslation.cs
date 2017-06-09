namespace ModLocalizer.Framework
{
	public sealed class CustomTranslation : ITranslation
	{
		public string TypeName => null;

		public string Namespace { get; set; } = string.Empty;

		public string Key { get; set; } = string.Empty;

		public string Value { get; set; } = string.Empty;
	}
}
