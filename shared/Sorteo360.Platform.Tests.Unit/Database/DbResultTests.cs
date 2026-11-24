using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Sorteo360.Platform.Database;
using Sorteo360.Platform.Errors;
using Npgsql;
using Shouldly;

namespace Sorteo360.Platform.Tests.Unit.Database;

/// <summary>
/// Traducción de fallos de persistencia a <see cref="BaseError"/>: ninguna excepción
/// de EF Core o Npgsql debe escapar de un repositorio (salvo la cancelación), las
/// violaciones de constraint deben llegar al cliente como conflicto y no como error
/// interno, y el detalle interno de PostgreSQL nunca debe viajar en el mensaje.
/// </summary>
public sealed class DbResultTests
{
    private const string Action = "crear el registro";

    /// <summary>
    /// Construye un <see cref="PostgresException"/> con el SQLSTATE indicado y un
    /// mensaje que simula el detalle interno de PostgreSQL (nombre de constraint).
    /// </summary>
    private static PostgresException CreatePostgresException(string sqlState) =>
        new("duplicate key value violates unique constraint \"ux_quote_event_category\"",
            "ERROR", "ERROR", sqlState);

    #region Éxito

    [Fact]
    public async Task TryAsync_ReturnsSuccessWithValue_WhenOperationSucceeds()
    {
        var result = await DbResult.TryAsync(NullLogger.Instance, Action, () => Task.FromResult(42));

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
    }

    #endregion

    #region Conflictos (SQLSTATE)

    [Theory]
    [InlineData("23505", "DB_CONFLICT", ErrorKind.Conflict)]  // unique violation
    [InlineData("23503", "DB_CONFLICT", ErrorKind.Conflict)]  // foreign key violation
    [InlineData("23514", "DB_ERROR", ErrorKind.Internal)]     // check violation: bug de validación previa
    [InlineData("42P01", "DB_ERROR", ErrorKind.Internal)]     // undefined table
    public async Task TryAsync_ReturnsTheErrorForTheSqlState_WhenAWriteThrowsAPostgresException(string sqlState, string expectedCode, ErrorKind expectedKind)
    {
        var ex = new DbUpdateException("envoltorio EF", CreatePostgresException(sqlState));

        var result = await DbResult.TryAsync<int>(NullLogger.Instance, Action, () => throw ex);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(expectedCode);
        result.Error.Kind.ShouldBe(expectedKind);
    }

    [Fact]
    public async Task TryAsync_ReturnsDbConflict_WhenAReadThrowsAUniqueViolation()
    {
        // En lecturas el error del servidor llega sin envolver en DbUpdateException.
        var result = await DbResult.TryAsync<int>(NullLogger.Instance, Action, () => throw CreatePostgresException("23505"));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("DB_CONFLICT");
        result.Error.Kind.ShouldBe(ErrorKind.Conflict);
    }

    [Fact]
    public async Task TryAsync_ReturnsAMessageWithoutThePostgresDetail_WhenPostgresRejectsTheWrite()
    {
        var ex = new DbUpdateException("envoltorio EF", CreatePostgresException("23505"));

        var result = await DbResult.TryAsync<int>(NullLogger.Instance, Action, () => throw ex);

        result.IsFailure.ShouldBeTrue();
        result.Error.Message.ShouldNotContain("ux_quote_event_category");
        result.Error.Message.ShouldNotContain("duplicate key");
        result.Error.Message.ShouldNotContain("envoltorio EF");
    }

    #endregion

    #region Concurrencia optimista

    [Fact]
    public async Task TryAsync_ReturnsConcurrencyConflict_WhenTheOperationThrowsDbUpdateConcurrencyException()
    {
        var ex = new DbUpdateConcurrencyException("la fila fue modificada por otra transacción");

        var result = await DbResult.TryAsync<int>(NullLogger.Instance, Action, () => throw ex);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("CONCURRENCY_CONFLICT");
        result.Error.Kind.ShouldBe(ErrorKind.Conflict);
        result.Error.Message.ShouldNotContain("la fila fue modificada por otra transacción");
    }

    #endregion

    #region Errores de base de datos no clasificados

    [Fact]
    public async Task TryAsync_ReturnsInternalDbError_WhenADbUpdateExceptionHasNoPostgresInner()
    {
        var ex = new DbUpdateException("fallo directo de EF", new Exception("detalle interno"));

        var result = await DbResult.TryAsync<int>(NullLogger.Instance, Action, () => throw ex);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("DB_ERROR");
        result.Error.Kind.ShouldBe(ErrorKind.Internal);
        result.Error.Message.ShouldNotContain("detalle interno");
    }

    [Fact]
    public async Task TryAsync_ReturnsInternalDbError_WhenTheOperationThrowsNpgsqlException()
    {
        var result = await DbResult.TryAsync<int>(NullLogger.Instance, Action, () => throw new NpgsqlException("conexión caída"));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("DB_ERROR");
        result.Error.Kind.ShouldBe(ErrorKind.Internal);
        result.Error.Message.ShouldNotContain("conexión caída");
    }

    #endregion

    #region Errores inesperados

    [Fact]
    public async Task TryAsync_ReturnsUnexpectedError_WhenTheOperationThrowsAnUnexpectedException()
    {
        var result = await DbResult.TryAsync<int>(NullLogger.Instance, Action, () => throw new InvalidOperationException("algo raro"));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("UNEXPECTED_ERROR");
        result.Error.Kind.ShouldBe(ErrorKind.Internal);
        result.Error.Message.ShouldNotContain("algo raro");
    }

    #endregion

    #region Cancelación y guardas

    [Fact]
    public async Task TryAsync_Rethrows_WhenOperationIsCancelled()
    {
        await Should.ThrowAsync<OperationCanceledException>(() =>
            DbResult.TryAsync<int>(NullLogger.Instance, Action, () => throw new OperationCanceledException()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TryAsync_Throws_WhenActionIsBlank(string action)
    {
        await Should.ThrowAsync<ArgumentException>(() =>
            DbResult.TryAsync(NullLogger.Instance, action, () => Task.FromResult(1)));
    }

    [Fact]
    public async Task TryAsync_Throws_WhenLoggerIsNull()
    {
        await Should.ThrowAsync<ArgumentNullException>(() =>
            DbResult.TryAsync(null!, Action, () => Task.FromResult(1)));
    }

    [Fact]
    public async Task TryAsync_Throws_WhenOperationIsNull()
    {
        await Should.ThrowAsync<ArgumentNullException>(() =>
            DbResult.TryAsync<int>(NullLogger.Instance, Action, null!));
    }

    #endregion

    #region TryResultAsync

    [Fact]
    public async Task TryResultAsync_ReturnsSuccessWithValue_WhenOperationSucceeds()
    {
        var result = await DbResult.TryResultAsync(NullLogger.Instance, Action,
            () => Task.FromResult(Result.Success<int, BaseError>(42)));

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
    }

    [Fact]
    public async Task TryResultAsync_PreservesFailure_WhenOperationFailsItsOwnPrecondition()
    {
        // La precondición se comprueba dentro de la misma transacción y construye su
        // propio error: debe llegar al llamante intacto, sin re-traducir.
        var precondition = new BaseError("El evento ya no admite escrituras.", "EVENT_CLOSED", ErrorKind.Conflict);

        var result = await DbResult.TryResultAsync(NullLogger.Instance, Action,
            () => Task.FromResult(Result.Failure<int, BaseError>(precondition)));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(precondition);
    }

    [Fact]
    public async Task TryResultAsync_ReturnsDbConflictWithoutPostgresDetail_WhenAWriteThrowsAUniqueViolation()
    {
        var ex = new DbUpdateException("envoltorio EF", CreatePostgresException("23505"));

        var result = await DbResult.TryResultAsync<int>(NullLogger.Instance, Action, () => throw ex);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("DB_CONFLICT");
        result.Error.Kind.ShouldBe(ErrorKind.Conflict);
        result.Error.Message.ShouldNotContain("ux_quote_event_category");
    }

    [Fact]
    public async Task TryResultAsync_Rethrows_WhenOperationIsCancelled()
    {
        await Should.ThrowAsync<OperationCanceledException>(() =>
            DbResult.TryResultAsync<int>(NullLogger.Instance, Action, () => throw new OperationCanceledException()));
    }

    #endregion
}
