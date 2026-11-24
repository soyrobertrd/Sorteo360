using Sorteo360.Convocatorias.Core.Uploads;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Uploads;

/// <summary>
/// El mapeo de columnas es <b>provisional</b> y vive en un solo sitio (spec <c>05</c> §8), así
/// que lo que se fija acá no es la lista de títulos sino que el cotejo perdone lo que un título
/// trae de adorno. El día que llegue el archivo real del cliente, esta suite sigue valiendo.
/// </summary>
public sealed class UploadColumnMapTests
{
    [Theory]
    [InlineData("Correo", UploadColumn.Email)]
    [InlineData("correo electrónico", UploadColumn.Email)]
    [InlineData("CORREO_ELECTRONICO", UploadColumn.Email)]
    [InlineData("  Email  ", UploadColumn.Email)]
    [InlineData("Teléfono", UploadColumn.Phone)]
    [InlineData("telefono", UploadColumn.Phone)]
    [InlineData("Primer Nombre", UploadColumn.FirstName)]
    [InlineData("primer-nombre", UploadColumn.FirstName)]
    [InlineData("Apellidos", UploadColumn.LastName)]
    [InlineData("Empresa", UploadColumn.Company)]
    public void Resolve_ReturnsTheColumn_WhenTheHeaderVariesInCaseAccentsOrSeparators(string header, UploadColumn expected)
    {
        UploadColumnMap.Resolve(header).ShouldBe(expected);
    }

    /// <summary>
    /// Una columna que no sirve no es un error: la hoja del cliente puede traer las suyas y se
    /// descartan sin ruido.
    /// </summary>
    [Theory]
    [InlineData("Observaciones")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Resolve_ReturnsNull_WhenTheHeaderIsUnknown(string? header)
    {
        UploadColumnMap.Resolve(header).ShouldBeNull();
    }
}
