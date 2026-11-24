namespace Sorteo360.Events.Core.Events;

/// <summary>Los seis montajes del salón que puede tener un evento.</summary>
/// <remarks>
/// El montaje del salón dice cómo se dispone la sala, y con eso si el evento tiene asientos y cómo
/// se reparten. No es la fecha de montaje de una propuesta, que en español también se llama
/// montaje. Es opcional en todo tipo de evento: un evento sin montaje del salón es uno en el que
/// nadie lo eligió todavía, y eso no es lo mismo que <see cref="Cocktail"/>, que es un salón de pie
/// elegido a propósito. Es un conjunto cerrado de cadenas, como <see cref="EventType"/>.
/// </remarks>
public static class SeatingLayout
{
    /// <summary>El banquete sienta a los invitados en mesas redondas, como en una cena.</summary>
    public const string Banquet    = "Banquet";

    /// <summary>El auditorio sienta a los invitados en filas, de frente a un escenario.</summary>
    /// <remarks>Es el mismo montaje en el que se mide el aforo en auditorio de un Venue.</remarks>
    public const string Auditorium = "Auditorium";

    /// <summary>La conferencia agrega al auditorio una mesa principal sobre el escenario.</summary>
    public const string Conference = "Conference";

    /// <summary>El cóctel deja a los invitados de pie, sin asientos.</summary>
    public const string Cocktail   = "Cocktail";

    /// <summary>La herradura dispone las mesas en forma de U.</summary>
    public const string UShape     = "UShape";

    /// <summary>El tipo escuela pone mesas pequeñas de frente a quien expone.</summary>
    public const string Classroom  = "Classroom";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Banquet, Auditorium, Conference, Cocktail, UShape, Classroom
    };
}
