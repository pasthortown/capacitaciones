namespace Capacitaciones.Domain.Entities;

/// <summary>
/// Estado del envío del certificado por correo a un asistente. Permite controlar,
/// asistente por asistente, el avance del proceso de "Generar y enviar certificados"
/// que corre en segundo plano:
///   <c>Pendiente</c> — marcado como elegible; aún no se ha enviado (o se va a reintentar).
///   <c>Enviado</c>   — el correo con el PDF se entregó a mail_sender con éxito.
///   <c>Error</c>     — falló la generación o el envío tras agotar los reintentos.
/// Se persiste como int nullable: <c>null</c> = no aplica (asistente no elegible: ausente
/// o sin marcar) o el evento nunca disparó un envío.
/// </summary>
public enum EstadoEnvioCertificado
{
    Pendiente = 1,
    Enviado = 2,
    Error = 3
}
