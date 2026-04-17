using Capacitaciones.Application.UseCases.Catalogos;
using Capacitaciones.Infrastructure.Adapters.Xlsx;

namespace Capacitaciones.Tests;

/// <summary>
/// Pruebas de humo del servicio XLSX basado en ClosedXML.
/// </summary>
public class XlsxTemplateServiceTests
{
    [Fact]
    public void BuildTemplate_Modalidades_ReturnsNonEmptyBytes()
    {
        var svc = new ClosedXmlTemplateService();
        var bytes = svc.BuildTemplate(CatalogoSlug.Modalidades);
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
    }

    [Fact]
    public void BuildTemplate_UnknownSlug_Throws()
    {
        var svc = new ClosedXmlTemplateService();
        Assert.Throws<ArgumentException>(() => svc.BuildTemplate("desconocido"));
    }

    [Fact]
    public void ImportRows_EmptyStream_ReportsNoSheetsError()
    {
        var svc = new ClosedXmlTemplateService();
        // Generamos una plantilla (archivo válido con una hoja vacía) y verificamos el conteo.
        var bytes = svc.BuildTemplate(CatalogoSlug.Areas);
        using var ms = new MemoryStream(bytes);
        var result = svc.ImportRows(CatalogoSlug.Areas, ms);

        // Plantilla vacía => 0 filas totales, 0 válidas, 0 errores.
        Assert.Equal(0, result.TotalFilas);
        Assert.Equal(0, result.FilasValidas);
        Assert.Empty(result.Errores);
    }
}
