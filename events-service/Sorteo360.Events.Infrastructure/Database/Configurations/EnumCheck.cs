namespace Sorteo360.Events.Infrastructure.Database.Configurations;

/// <summary>Arma la lista de valores que un CHECK admite para una columna que guarda un enum como texto.</summary>
/// <remarks>
/// La lista sale de los nombres del enum, así que un valor nuevo cambia el modelo y pide su migración.
/// Los nombres llegan siempre en el orden en que se declaran, y por eso el texto del CHECK no cambia de
/// una ejecución a otra ni genera migraciones fantasma.
/// </remarks>
internal static class EnumCheck
{
    public static string AllowedValues<TEnum>()
        where TEnum : struct, Enum =>
        string.Join(", ", Enum.GetNames<TEnum>().Select(name => $"'{name}'"));
}
