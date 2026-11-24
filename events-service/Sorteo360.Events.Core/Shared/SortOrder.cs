namespace Sorteo360.Events.Core.Shared;

/// <summary>
/// El sentido de un orden. Viaja por el query string como <c>asc</c> o <c>desc</c>, sin
/// distinguir mayúsculas.
/// </summary>
public enum SortOrder
{
    Asc,
    Desc
}
