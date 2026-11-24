using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Sorteo360.Platform.Errors;
using Sorteo360.Platform.GlobalEndpoints;
using Shouldly;

namespace Sorteo360.Platform.Tests.Unit.GlobalEndpoints;

/// <summary>
/// La tabla <see cref="ErrorKind"/> → estado HTTP vive en un solo sitio, así que se
/// verifica entera: un fallo mal clasificado convierte un problema de otro servicio
/// en un 500 propio, que es justo lo que <see cref="ErrorKind.Unavailable"/> evita.
/// </summary>
public sealed class BaseErrorExtensionsTests
{
    [Theory]
    [InlineData(ErrorKind.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ErrorKind.Invalid, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorKind.Forbidden, StatusCodes.Status403Forbidden)]
    [InlineData(ErrorKind.Conflict, StatusCodes.Status409Conflict)]
    [InlineData(ErrorKind.Unavailable, StatusCodes.Status503ServiceUnavailable)]
    [InlineData(ErrorKind.Unauthenticated, StatusCodes.Status401Unauthorized)]
    [InlineData(ErrorKind.TooManyRequests, StatusCodes.Status429TooManyRequests)]
    [InlineData(ErrorKind.Internal, StatusCodes.Status500InternalServerError)]
    public void ToProblem_ReturnsTheMappedStatus_WhenGivenAnErrorKind(ErrorKind kind, int expectedStatusCode)
    {
        var error = new BaseError("mensaje para el cliente", "SOME_CODE", kind);

        var result = error.ToProblem();

        var problem = result.ShouldBeOfType<ProblemHttpResult>();
        problem.StatusCode.ShouldBe(expectedStatusCode);
        problem.ProblemDetails.Detail.ShouldBe("mensaje para el cliente");
    }

    [Fact]
    public void ToProblem_ReturnsTheCodeAsAnExtension_WhenTheErrorHasOne()
    {
        // Dos fallos con el mismo estado se distinguen por el código y no por el mensaje, que es
        // prosa en español para una persona. Quien consume decide qué decir a partir del código.
        var error = new BaseError("La oficina OPT ya no está vigente.", "OPT_OFFICE_NOT_AVAILABLE", ErrorKind.Conflict);

        var problem = error.ToProblem().ShouldBeOfType<ProblemHttpResult>();

        problem.ProblemDetails.Extensions["code"].ShouldBe("OPT_OFFICE_NOT_AVAILABLE");
    }

    [Fact]
    public void ToProblem_OmitsTheCode_WhenTheErrorHasNone()
    {
        var error = new BaseError("fallo sin clasificar");

        var problem = error.ToProblem().ShouldBeOfType<ProblemHttpResult>();

        problem.ProblemDetails.Extensions.ShouldNotContainKey("code");
    }

    [Fact]
    public void ToProblem_Throws_WhenErrorIsNull()
    {
        BaseError error = null!;

        Should.Throw<ArgumentNullException>(() => error.ToProblem());
    }

    [Fact]
    public void ToProblem_MergesTheExtensions_WhenTheErrorContributesThem()
    {
        // Un 409 por correo repetido lleva el contacto que ya lo ocupa, para que quien consume
        // resuelva el choque sin una segunda petición.
        var error = new ContributingError(new Dictionary<string, object?>
        {
            ["existingContact"] = new { Id = 7 },
            ["retryAfterSeconds"] = 30
        });

        var problem = error.ToProblem().ShouldBeOfType<ProblemHttpResult>();

        problem.ProblemDetails.Extensions["code"].ShouldBe("SOME_CODE");
        problem.ProblemDetails.Extensions["retryAfterSeconds"].ShouldBe(30);
        problem.ProblemDetails.Extensions.ShouldContainKey("existingContact");
    }

    [Fact]
    public void ProblemExtensions_IsEmpty_WhenNoSubclassOverridesIt()
    {
        new BaseError("fallo sin clasificar").ProblemExtensions.ShouldBeEmpty();
    }

    [Fact]
    public void ToProblem_LeavesTheResponseUntouched_WhenTheErrorContributesNothing()
    {
        // La costura es aditiva: un error que no aporta nada responde exactamente lo que
        // respondía antes de que existiera, y por eso ningún otro servicio cambia.
        var contributing = new ContributingError(new Dictionary<string, object?>());
        var plain = new BaseError("mensaje para el cliente", "SOME_CODE", ErrorKind.Conflict);

        var fromContributing = contributing.ToProblem().ShouldBeOfType<ProblemHttpResult>();
        var fromPlain = plain.ToProblem().ShouldBeOfType<ProblemHttpResult>();

        fromContributing.StatusCode.ShouldBe(fromPlain.StatusCode);
        fromContributing.ProblemDetails.Detail.ShouldBe(fromPlain.ProblemDetails.Detail);
        fromContributing.ProblemDetails.Extensions.ShouldBe(fromPlain.ProblemDetails.Extensions);
    }

    [Fact]
    public void ToProblem_OmitsTheExtensions_WhenThereIsNeitherCodeNorContribution()
    {
        var error = new ContributingError(new Dictionary<string, object?>(), code: null);

        var problem = error.ToProblem().ShouldBeOfType<ProblemHttpResult>();

        problem.ProblemDetails.Extensions.ShouldBeEmpty();
    }

    [Fact]
    public void ToProblem_KeepsItsOwnCode_WhenAnErrorContributesOne()
    {
        // El código es del mapeador: una subclase que aporte la clave "code" no puede suplantar
        // el identificador por el que rama quien consume.
        var error = new ContributingError(new Dictionary<string, object?>
        {
            ["code"] = "IMPOSTOR_CODE"
        });

        var problem = error.ToProblem().ShouldBeOfType<ProblemHttpResult>();

        problem.ProblemDetails.Extensions["code"].ShouldBe("SOME_CODE");
    }

    [Fact]
    public void ToProblem_DropsAContributedCode_WhenTheErrorHasNoneOfItsOwn()
    {
        var error = new ContributingError(
            new Dictionary<string, object?> { ["code"] = "IMPOSTOR_CODE" },
            code: null);

        var problem = error.ToProblem().ShouldBeOfType<ProblemHttpResult>();

        problem.ProblemDetails.Extensions.ShouldNotContainKey("code");
    }

    private sealed record ContributingError : BaseError
    {
        private readonly IReadOnlyDictionary<string, object?> _extensions;

        public ContributingError(IReadOnlyDictionary<string, object?> extensions, string? code = "SOME_CODE")
            : base("mensaje para el cliente", code, ErrorKind.Conflict)
        {
            _extensions = extensions;
        }

        public override IReadOnlyDictionary<string, object?> ProblemExtensions => _extensions;
    }
}
