namespace BCB.Cotizaciones.Api.Models;

public sealed class BcbOptions
{
    public const string SectionName = "BCB";

    public decimal TipoCambioBase { get; set; } = 6.9600m;
    public string ClaveAdministracion { get; set; } = string.Empty;
}
