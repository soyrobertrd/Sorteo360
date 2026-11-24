using CSharpFunctionalExtensions;

namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// Quién es miembro de DO Travel Awards. El programa es del cliente y no se puede consultar, así que
/// es un seam: nada en esta firma dice de dónde sale la lista (spec 01 §5).
/// </summary>
public interface IAwardsMembership
{
    /// <summary>
    /// La lista en vigor, entera, en una sola lectura por cálculo.
    /// <para>
    /// <c>None</c> quiere decir que no hay opinión, y el paso de DO Travel Awards se salta entero.
    /// Nunca quiere decir "nadie es miembro": eso es una lista en vigor y vacía, que no califica a
    /// nadie (spec 01 §5.2). Confundir las dos cosas dejaría el bombo vacío, sin motivo a la vista,
    /// cada vez que no se hubiera cargado ninguna lista.
    /// </para>
    /// </summary>
    Task<Maybe<AwardsList>> GetInForceAsync(CancellationToken cancellationToken);
}
