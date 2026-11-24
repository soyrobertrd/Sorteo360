using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.Templates;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Database;

/// <summary>
/// Lectura de la tabla de plantillas. <b>No hay escritura en fase 1</b>: el conjunto lo siembra
/// una migración de datos y nadie edita una plantilla desde la API.
/// <para>
/// Que la tabla exista hoy es lo que hace aditivos los endpoints de administración del día que
/// MITUR gestione sus propias plantillas, que es un requisito declarado y no una hipótesis.
/// </para>
/// </summary>
public interface ITemplateRepository
{
    /// <summary>
    /// Las plantillas, sin cuerpo, ordenadas por clave e idioma.
    /// </summary>
    /// <param name="selectableOnly">
    /// Si solo las que una campaña puede elegir. Es para lo que existe la columna: sin este
    /// filtro el selector del frontend tendría que separar la lista por su cuenta.
    /// </param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<Result<IReadOnlyList<TemplateSummary>, BaseError>> ListAsync(
        bool selectableOnly,
        CancellationToken cancellationToken);

    /// <summary>
    /// Una plantilla entera, asunto y cuerpo, por su clave y su idioma. <see cref="Maybe{T}.None"/>
    /// si esa combinación no está sembrada.
    /// <para>
    /// Es la única lectura que trae el cuerpo, y existe para el envío: el worker necesita el correo
    /// completo, con sus marcadores intactos, para pasárselo al transporte (spec <c>04</c> §2). El
    /// listado sigue sin traerlo a propósito.
    /// </para>
    /// <para>
    /// Que falte la variante de un idioma no se resuelve cayendo al español: el lanzamiento se
    /// queda bloqueado hasta que alguien escriba la copia (spec <c>02</c> §3).
    /// </para>
    /// </summary>
    Task<Result<Maybe<Template>, BaseError>> GetAsync(
        string templateKey,
        string language,
        CancellationToken cancellationToken);
}
