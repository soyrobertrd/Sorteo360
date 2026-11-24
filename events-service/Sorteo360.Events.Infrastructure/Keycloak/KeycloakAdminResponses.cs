namespace Sorteo360.Events.Infrastructure.Keycloak;

/// <summary>
/// Un rol del realm tal como lo devuelve la Admin API, recortado a lo que hace falta para invertir
/// el grafo: cómo se llama y si es compuesto.
/// <para>
/// La representación breve —la que devuelve el listado— trae <c>composite</c> y es lo único que se
/// mira para decidir a quién hay que pedirle los hijos.
/// </para>
/// </summary>
public sealed record KeycloakRoleResponse(string Name, bool Composite);

/// <summary>
/// Una persona del realm tal como la devuelve la Admin API.
/// <para>
/// <b>El <c>opt_code</c> no viene acá y no es un error.</b> El realm no siembra configuración de
/// perfil de usuario, así que es un atributo no gestionado y la política por defecto lo filtra de
/// todas las respuestas. Funciona perfecto como filtro (<c>q=opt_code:miami</c> pega contra el
/// atributo) y es invisible al leerlo de vuelta. Todo este enfoque sobrevive precisamente porque
/// filtra por él y nunca lo lee: quien depure esto va a hacer un GET, no va a ver el
/// <c>opt_code</c>, y va a concluir que el directorio está mal. No lo está.
/// </para>
/// </summary>
public sealed record KeycloakUserResponse(
    string Id,
    string? Username,
    string? Email,
    string? FirstName,
    string? LastName,
    bool Enabled)
{
    /// <summary>
    /// Con qué nombre se saluda a esta persona: nombre y apellido si los tiene cargados, y si no,
    /// su usuario del realm. Nunca queda vacío, porque un correo dirigido a nadie se lee como un
    /// error del sistema y no como un aviso.
    /// </summary>
    public string DisplayName()
    {
        var fullName = $"{FirstName} {LastName}".Trim();

        return fullName.Length > 0 ? fullName : Username ?? Id;
    }
}
