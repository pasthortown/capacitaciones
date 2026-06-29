namespace Capacitaciones.Application.Ports;

/// <summary>
/// Almacenamiento físico del anexo de un convenio (el convenio firmado por el colaborador).
/// El adaptador por defecto escribe en <c>CONVENIOS_DIR</c> (default <c>/convenios_anexos</c>).
/// Convención de <c>storedName</c>: nombre plano <c>{guid}.{ext}</c>.
/// </summary>
public interface IConvenioAnexoStorage
{
    Task SaveAsync(Stream content, string storedName, CancellationToken ct);
    bool Exists(string storedName);
    Task DeleteAsync(string storedName, CancellationToken ct);
    Stream OpenRead(string storedName);
}
