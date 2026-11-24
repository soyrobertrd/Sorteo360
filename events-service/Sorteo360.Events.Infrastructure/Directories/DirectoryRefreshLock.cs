namespace Sorteo360.Events.Infrastructure.Directories;

/// <summary>
/// Serializa la renovación de una copia de directorio.
/// <para>
/// Sin esto, veinticinco lecturas simultáneas sobre una caché fría dispararían veinticinco
/// consultas al origen en lugar de una. Es un objeto inyectado y no un campo estático porque su
/// alcance tiene que ser el mismo que el de la caché que protege: uno por contenedor, no uno por
/// proceso.
/// </para>
/// <para>
/// Cada directorio trae su propio cerrojo —de ahí las subclases— porque uno compartido haría que
/// renovar las empresas esperara a que terminara de renovarse OPT, dos consultas que no compiten
/// por nada. Son tipos distintos y no una instancia con nombre porque así el contenedor los
/// distingue sin claves.
/// </para>
/// <para>
/// Vive en una carpeta que no nombra ningún origen porque no es de ninguno: dos de estos cerrojos
/// protegen copias que se renuevan contra el CRM y dos contra el realm de Keycloak.
/// </para>
/// </summary>
public abstract class DirectoryRefreshLock : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>Espera el turno para renovar.</summary>
    public Task WaitAsync(CancellationToken cancellationToken) => _semaphore.WaitAsync(cancellationToken);

    /// <summary>Cede el turno.</summary>
    public void Release() => _semaphore.Release();

    /// <inheritdoc />
    public void Dispose() => _semaphore.Dispose();
}

/// <summary>El cerrojo de la copia del directorio de OPT, que se renueva contra el CRM.</summary>
public sealed class OptDirectoryRefreshLock : DirectoryRefreshLock;

/// <summary>El cerrojo de la copia del directorio de empresas, que se renueva contra el CRM.</summary>
public sealed class CompanyDirectoryRefreshLock : DirectoryRefreshLock;

/// <summary>
/// El cerrojo de las copias de la resolución de aprobadores, que se renuevan contra el realm.
/// <para>
/// Uno solo para las dos copias —el grafo y la pertenencia— y no dos, porque se renuevan en la
/// misma llamada y una detrás de la otra: dos cerrojos no dejarían pasar a nadie antes y sí
/// permitirían que dos peticiones recorrieran el grafo a la vez.
/// </para>
/// </summary>
public sealed class QuoteApproverRefreshLock : DirectoryRefreshLock;

/// <summary>
/// El cerrojo de la copia de quien sometió un intento, que se renueva contra el realm.
/// <para>
/// Es el suyo y no el de <see cref="QuoteApproverRefreshLock"/>, siguiendo la regla que enuncia la
/// clase base: uno por directorio. Compartirlo haría que una aprobación esperara a que terminara
/// de recorrerse el grafo de roles de otro sometimiento, y son dos consultas que no compiten por
/// nada.
/// </para>
/// </summary>
public sealed class QuoteSubmitterRefreshLock : DirectoryRefreshLock;

/// <summary>
/// El cerrojo de las copias de la lista de la puerta de un sorteo, que se renuevan contra
/// convocatorias. Uno para todos los eventos: rara vez hay dos sorteos armándose a la vez, y lo que
/// evita es que cada pantalla que consulta dispare su propia lectura al vencer la copia.
/// </summary>
public sealed class RaffleAttendanceRefreshLock : DirectoryRefreshLock;
