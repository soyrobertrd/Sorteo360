namespace Sorteo360.Convocatorias.Infrastructure.Crm;

/// <summary>
/// Lo que contestó una lectura por id del CRM. Separa los dos vacíos que el transporte mezcla:
/// un <c>404</c> es "no la tiene" (<see cref="Found"/> falso) y no un fallo, mientras que un
/// <c>200</c> con el cuerpo vacío es un CRM que contestó sin decir nada, y eso sí lo es.
/// </summary>
/// <typeparam name="T">La forma de la respuesta.</typeparam>
/// <param name="Found">Falso cuando el CRM contestó <c>404</c>.</param>
/// <param name="Body">El cuerpo leído; nulo si no vino, aunque <see cref="Found"/> sea cierto.</param>
public sealed record CrmByIdRead<T>(bool Found, T? Body)
    where T : class;
