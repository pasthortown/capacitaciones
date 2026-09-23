namespace Capacitaciones.Application.UseCases.Configuracion;

/// <summary>Definición fija de un tipo de notificación existente en el sistema.</summary>
public sealed record NotificacionDefinicion(
    string Plantilla,
    string Nombre,
    string AsuntoActual,
    IReadOnlyList<string> Variables);

/// <summary>
/// Catálogo de las notificaciones que envían el backend y event_monitor. Es la fuente
/// del seed de <c>ConfiguracionNotificacion</c> y de las variables que muestra la UI.
/// Al agregar una plantilla nueva en mail_sender, agregarla aquí y crear una migración.
/// </summary>
public static class NotificacionesCatalogo
{
    /// <summary>Variable disponible en todos los asuntos: el asunto que habría usado el sistema.</summary>
    public const string VariableAsuntoOriginal = "asunto_original";

    public static IReadOnlyList<NotificacionDefinicion> Todas { get; } = new[]
    {
        new NotificacionDefinicion("invitacion_inscripcion", "Invitación de inscripción",
            "{tipo} Creado / {tipo} Actualizado / Invitación a {tipo}: {tema}",
            new[] { "tema", "tipoActividad", "fecha", "hora", "duracion", "modalidad", "capacitador" }),
        new NotificacionDefinicion("capacitador_descripcion", "Capacitador: cargar información del curso",
            "Cargar información del curso: {tema}",
            new[] { "nombre", "tema", "link" }),
        new NotificacionDefinicion("capacitador_pase_lista", "Capacitador: pase de lista",
            "Pase de lista: {tema}",
            new[] { "nombre", "tema", "link" }),
        new NotificacionDefinicion("responsable_firma", "Responsable: carga de datos y firma",
            "Carga tus datos y firma en CapacitaDOS",
            new[] { "nombre", "link" }),
        new NotificacionDefinicion("registro_asistencia_admin", "Reporte de asistencia al admin",
            "Registro de asistencia: {tema}",
            new[] { "tema", "codigo", "fecha" }),
        new NotificacionDefinicion("certificado_participante", "Certificado al participante",
            "Tu certificado: {tema}",
            new[] { "nombre", "tema", "fecha" }),
        new NotificacionDefinicion("recordatorio_inicio_proximo", "Recordatorio: capacitación por iniciar",
            "Recordatorio: tu capacitación inicia pronto - {tema}",
            new[] { "nombre", "tema", "fecha", "hora", "modalidad" }),
        new NotificacionDefinicion("recordatorio_evento_iniciado", "Aviso: capacitación iniciada",
            "Tu capacitación ya inició: {tema}",
            new[] { "nombre", "tema", "modalidad" }),
        new NotificacionDefinicion("encuesta_satisfaccion", "Encuesta de satisfacción",
            "Cuéntanos tu experiencia: {tema}",
            new[] { "nombre", "tema", "link" }),
    };

    public static NotificacionDefinicion? Buscar(string plantilla) =>
        Todas.FirstOrDefault(d => string.Equals(d.Plantilla, plantilla, StringComparison.Ordinal));
}
