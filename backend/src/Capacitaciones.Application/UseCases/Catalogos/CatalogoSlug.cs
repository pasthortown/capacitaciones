namespace Capacitaciones.Application.UseCases.Catalogos;

/// <summary>
/// Slugs canónicos de los catálogos administrables (expuestos en la URL).
/// </summary>
public static class CatalogoSlug
{
    public const string Modalidades = "modalidades";
    public const string TiposActividad = "tipos-actividad";
    public const string Areas = "areas";

    public static readonly string[] All = { Modalidades, TiposActividad, Areas };

    public static bool IsKnown(string slug) => Array.IndexOf(All, slug) >= 0;
}
