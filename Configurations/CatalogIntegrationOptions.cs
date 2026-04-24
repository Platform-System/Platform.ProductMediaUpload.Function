namespace Platform.ProductMediaUpload.Function.Configurations;

public sealed class CatalogIntegrationOptions
{
    public const string SectionName = "Integrations:Catalog";

    public string Address { get; set; } = string.Empty;
}
