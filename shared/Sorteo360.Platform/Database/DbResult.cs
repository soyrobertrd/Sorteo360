using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sorteo360.Platform.Errors;
using Npgsql;

namespace Sorteo360.Platform.Database;

/// <summary>
/// Seam que concentra el manejo de excepciones de acceso a datos de los repositorios.
/// Ejecuta una operación contra el <c>DbContext</c> del servicio y traduce cualquier fallo
/// de EF Core / Npgsql a un <see cref="BaseError"/> uniforme, de modo que las excepciones
/// de persistencia nunca se propagan a las capas superiores.
/// La excepción original se registra en el log; el mensaje que llega al cliente es
/// siempre genérico y en español (nunca expone detalles internos de PostgreSQL como
/// nombres de constraints o de columnas).
/// </summary>
public static class DbResult
{
    private const string DbErrorCode = "DB_ERROR";
    private const string DbConflictCode = "DB_CONFLICT";
    private const string ConcurrencyConflictCode = "CONCURRENCY_CONFLICT";
    private const string UnexpectedErrorCode = "UNEXPECTED_ERROR";

    // Códigos SQLSTATE de PostgreSQL relevantes para la traducción de errores.
    private const string UniqueViolation = "23505";
    private const string ForeignKeyViolation = "23503";
    private const string CheckViolation = "23514";

    /// <summary>
    /// Ejecuta <paramref name="operation"/> y envuelve su valor en un resultado exitoso.
    /// Los fallos se clasifican por su naturaleza:
    /// <list type="bullet">
    /// <item><see cref="DbUpdateConcurrencyException"/> (token de concurrencia optimista) se
    /// traduce a <c>CONCURRENCY_CONFLICT</c> con <see cref="ErrorKind.Conflict"/> (HTTP 409).</item>
    /// <item><see cref="PostgresException"/> con SQLSTATE 23505 (unique) o 23503 (foreign key)
    /// se traduce a <c>DB_CONFLICT</c> con <see cref="ErrorKind.Conflict"/> (HTTP 409). Es lo
    /// que convierte una colisión de constraint en un 409 y no en un 500.</item>
    /// <item>Cualquier otro fallo de base de datos se traduce a <c>DB_ERROR</c> con
    /// <see cref="ErrorKind.Internal"/> (HTTP 500); el resto de excepciones a
    /// <c>UNEXPECTED_ERROR</c>.</item>
    /// <item><see cref="OperationCanceledException"/> se re-lanza intacta para respetar la
    /// cancelación cooperativa.</item>
    /// </list>
    /// </summary>
    /// <typeparam name="T">Tipo del valor producido por la operación.</typeparam>
    /// <param name="logger">Logger del repositorio; recibe la excepción original completa.</param>
    /// <param name="action">
    /// Frase en infinitivo que describe la operación (p. ej. "crear el registro"); se usa para
    /// construir el mensaje del error ("Error al {action}: …").
    /// </param>
    /// <param name="operation">Operación de acceso a datos a ejecutar.</param>
    public static async Task<Result<T, BaseError>> TryAsync<T>(ILogger logger, string action, Func<Task<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentNullException.ThrowIfNull(operation);

        try
        {
            var value = await operation();
            return Result.Success<T, BaseError>(value);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex, "Conflicto de concurrencia al {Action}", action);
            return Result.Failure<T, BaseError>(new BaseError(
                $"Error al {action}: el registro fue modificado por otra operación concurrente.",
                ConcurrencyConflictCode,
                ErrorKind.Conflict));
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg)
        {
            // Las escrituras envuelven el error real de PostgreSQL en InnerException.
            return FromPostgres<T>(logger, action, ex, pg);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Error de base de datos al {Action}", action);
            return FailInternal<T>(action);
        }
        catch (PostgresException ex)
        {
            // Errores del servidor en lecturas (llegan sin envolver).
            return FromPostgres<T>(logger, action, ex, ex);
        }
        catch (NpgsqlException ex)
        {
            // Fallos de conexión y demás errores del driver.
            logger.LogError(ex, "Error de base de datos al {Action}", action);
            return FailInternal<T>(action);
        }
        catch (OperationCanceledException)
        {
            // La cancelación no es un fallo de datos: se propaga tal cual.
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error inesperado al {Action}", action);
            return Result.Failure<T, BaseError>(new BaseError(
                $"Error inesperado al {action}.",
                UnexpectedErrorCode));
        }
    }

    /// <summary>
    /// Variante de <see cref="TryAsync{T}"/> para operaciones que además pueden fallar
    /// por una precondición comprobada <em>dentro</em> de la misma transacción (por
    /// ejemplo, que el agregado siga admitiendo escrituras): la operación construye su
    /// propio <see cref="Result{T, TError}"/> y el manejo de excepciones es idéntico.
    /// </summary>
    /// <typeparam name="T">Tipo del valor producido por la operación.</typeparam>
    /// <param name="logger">Logger del repositorio; recibe la excepción original completa.</param>
    /// <param name="action">Frase en infinitivo que describe la operación.</param>
    /// <param name="operation">Operación de acceso a datos a ejecutar.</param>
    public static async Task<Result<T, BaseError>> TryResultAsync<T>(ILogger logger, string action, Func<Task<Result<T, BaseError>>> operation)
    {
        var wrapped = await TryAsync(logger, action, operation);

        return wrapped.IsFailure
            ? Result.Failure<T, BaseError>(wrapped.Error)
            : wrapped.Value;
    }

    /// <summary>
    /// Traduce un <see cref="PostgresException"/> según su SQLSTATE: violaciones de
    /// unicidad y de foreign key son conflictos (409) con mensaje genérico; el resto
    /// (incluidas violaciones de CHECK, que indican un bug de validación previa) son
    /// fallos internos (500). El detalle original solo va al log.
    /// </summary>
    private static Result<T, BaseError> FromPostgres<T>(ILogger logger, string action, Exception original, PostgresException pg)
    {
        switch (pg.SqlState)
        {
            case UniqueViolation:
                logger.LogWarning(original, "Violación de unicidad al {Action}", action);
                return Result.Failure<T, BaseError>(new BaseError(
                    $"Error al {action}: ya existe un registro con los mismos datos.",
                    DbConflictCode,
                    ErrorKind.Conflict));

            case ForeignKeyViolation:
                logger.LogWarning(original, "Violación de foreign key al {Action}", action);
                return Result.Failure<T, BaseError>(new BaseError(
                    $"Error al {action}: la operación entra en conflicto con registros relacionados.",
                    DbConflictCode,
                    ErrorKind.Conflict));

            case CheckViolation:
            default:
                logger.LogError(original, "Error de base de datos al {Action} (SQLSTATE {SqlState})", action, pg.SqlState);
                return FailInternal<T>(action);
        }
    }

    /// <summary>
    /// Construye el fallo interno genérico de base de datos (<c>DB_ERROR</c>, HTTP 500)
    /// sin exponer detalles de la excepción original.
    /// </summary>
    private static Result<T, BaseError> FailInternal<T>(string action) =>
        Result.Failure<T, BaseError>(new BaseError($"Error al {action}: fallo de base de datos.", DbErrorCode));
}
