using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Sorteo360.Platform.Errors;
using Sorteo360.Platform.GlobalEndpoints;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.GlobalEndpoints;

/// <summary>
/// Cubre la única tabla <see cref="ErrorKind"/> → estado HTTP del servicio. Los mapeos
/// existentes se afirman junto al nuevo para que añadir un miembro al enum no desplace
/// en silencio a ninguno hacia el 500 del caso por defecto.
/// </summary>
public sealed class BaseErrorExtensionsTests
{
    /// <summary>Extrae el estado HTTP del <see cref="IResult"/> producido por ToProblem.</summary>
    private static int StatusCodeOf(BaseError error) =>
        error.ToProblem().ShouldBeAssignableTo<IStatusCodeHttpResult>()!.StatusCode!.Value;

    #region Tabla de mapeo

    [Theory]
    [InlineData(ErrorKind.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ErrorKind.Conflict, StatusCodes.Status409Conflict)]
    [InlineData(ErrorKind.Forbidden, StatusCodes.Status403Forbidden)]
    [InlineData(ErrorKind.Internal, StatusCodes.Status500InternalServerError)]
    public void ToProblem_ReturnsTheKindsHttpStatus_WhenTheKindIsMapped(ErrorKind kind, int expectedStatus)
    {
        var error = new BaseError("mensaje de dominio", kind: kind);

        StatusCodeOf(error).ShouldBe(expectedStatus);
    }

    [Fact]
    public void ToProblem_ReturnsANon500Status_WhenTheKindIsNotInternal()
    {
        // Guarda contra un miembro nuevo que caiga sin querer en la rama por defecto.
        foreach (var kind in Enum.GetValues<ErrorKind>().Where(k => k != ErrorKind.Internal))
        {
            StatusCodeOf(new BaseError("mensaje de dominio", kind: kind))
                .ShouldNotBe(StatusCodes.Status500InternalServerError);
        }
    }

    #endregion

    #region Contenido de la respuesta

    [Fact]
    public void ToProblem_ReturnsTheDomainMessage_WhenKindIsForbidden()
    {
        var error = new BaseError("No puede aprobar una cotización que usted mismo sometió", kind: ErrorKind.Forbidden);

        var problem = error.ToProblem().ShouldBeOfType<ProblemHttpResult>();

        problem.ProblemDetails.Detail.ShouldBe("No puede aprobar una cotización que usted mismo sometió");
        problem.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public void ToProblem_Throws_WhenErrorIsNull()
    {
        BaseError error = null!;

        Should.Throw<ArgumentNullException>(() => error.ToProblem());
    }

    #endregion
}
