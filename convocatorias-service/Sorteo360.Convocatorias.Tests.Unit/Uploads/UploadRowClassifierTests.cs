using Sorteo360.Convocatorias.Core.Uploads;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Uploads;

/// <summary>
/// Las dos reglas que se deciden sin salir del archivo (spec <c>05</c> §4 cubos 1 y 2, y §5).
/// Son puras, así que se prueban con una lista en memoria y sin base de datos ni CRM.
/// </summary>
public sealed class UploadRowClassifierTests
{
    [Fact]
    public void Classify_ReturnsMalformedWithMissingEmail_WhenTheRowHasNoEmail()
    {
        var classified = Classify(Row(2, "Ana", "Pérez", email: null));

        classified.Rows[0].Bucket.ShouldBe(UploadRowBucket.Malformed);
        classified.Rows[0].ReasonCode.ShouldBe(MalformedRowReason.MissingEmail);
    }

    [Theory]
    [InlineData("no-es-un-correo")]
    [InlineData("dos@@arrobas.com")]
    [InlineData("sin@dominio")]
    [InlineData("con espacio@acme.com")]
    public void Classify_ReturnsMalformedWithInvalidEmail_WhenTheEmailIsNotAnAddress(string email)
    {
        var classified = Classify(Row(2, "Ana", "Pérez", email));

        classified.Rows[0].Bucket.ShouldBe(UploadRowBucket.Malformed);
        classified.Rows[0].ReasonCode.ShouldBe(MalformedRowReason.InvalidEmail);
    }

    [Fact]
    public void Classify_ReturnsMalformedWithMissingName_WhenTheRowHasNoNameAtAll()
    {
        var classified = Classify(Row(2, null, null, "ana@acme.com"));

        classified.Rows[0].ReasonCode.ShouldBe(MalformedRowReason.MissingName);
    }

    /// <summary>
    /// Hacen falta los dos. El CRM exige nombre y apellido al crear un contacto, así que dar por
    /// buena media fila obligaría a inventar la otra mitad y escribirla en el sistema de registro.
    /// </summary>
    [Theory]
    [InlineData("Ana", null)]
    [InlineData(null, "Pérez")]
    [InlineData("Ana", "   ")]
    public void Classify_ReturnsMalformedWithMissingName_WhenOnlyHalfTheNameIsGiven(string? firstName, string? lastName)
    {
        var classified = Classify(Row(2, firstName, lastName, "ana@acme.com"));

        classified.Rows[0].Bucket.ShouldBe(UploadRowBucket.Malformed);
        classified.Rows[0].ReasonCode.ShouldBe(MalformedRowReason.MissingName);
    }

    /// <summary>Con los dos, la fila sigue viva y le toca preguntarle al CRM.</summary>
    [Fact]
    public void Classify_ReturnsNoBucket_WhenTheRowHasBothNamesAndAnAddress()
    {
        var classified = Classify(Row(2, "Ana", "Pérez", "ana@acme.com"));

        classified.Rows[0].Bucket.ShouldBeNull();
    }

    /// <summary>
    /// Sin correo y sin nombre: gana el primer motivo del orden de §4, que es el correo. Un
    /// motivo por fila, no una lista.
    /// </summary>
    [Fact]
    public void Classify_ReturnsTheFirstReasonInOrder_WhenSeveralReasonsApply()
    {
        var classified = Classify(Row(2, null, null, email: null));

        classified.Rows[0].ReasonCode.ShouldBe(MalformedRowReason.MissingEmail);
    }

    /// <summary>
    /// Tres filas idénticas se pliegan en una y se cuentan. Nadie tiene que elegir entre dos
    /// filas iguales, y el pegado repetido es el caso común.
    /// </summary>
    [Fact]
    public void Classify_ReturnsOneRowWithACollapsedCount_WhenThreeRowsAreIdentical()
    {
        var classified = Classify(
            Row(2, "Ana", "Pérez", "ana@acme.com"),
            Row(3, "Ana", "Pérez", "ana@acme.com"),
            Row(4, "Ana", "Pérez", "ana@acme.com"));

        classified.Rows.Count.ShouldBe(1);
        classified.Rows[0].Row.SourceRowNumber.ShouldBe(2);
        classified.Rows[0].CollapsedDuplicates.ShouldBe(2);
        classified.CollapsedDuplicates.ShouldBe(2);
    }

    /// <summary>La grafía del correo no rompe un pliegue: es la clave con la que se agrupó.</summary>
    [Fact]
    public void Classify_ReturnsOneCollapsedRow_WhenRowsDifferOnlyInEmailCase()
    {
        var classified = Classify(
            Row(2, "Ana", "Pérez", "ana@acme.com"),
            Row(3, "Ana", "Pérez", "ANA@Acme.com"));

        classified.Rows.Count.ShouldBe(1);
        classified.CollapsedDuplicates.ShouldBe(1);
    }

    /// <summary>
    /// Comparten correo y no dicen lo mismo: van todas al cubo, ninguna se crea, y el cubo nombra
    /// las filas del grupo para que quien subió pueda ir a mirarlas.
    /// </summary>
    [Fact]
    public void Classify_ReturnsDuplicateInFileNamingTheRows_WhenRowsShareAnEmailButDiffer()
    {
        var classified = Classify(
            Row(2, "Ana", "Pérez", "ana@acme.com"),
            Row(3, "Ana", "Pérez Gómez", "ana@acme.com"),
            Row(4, "Ana María", "Pérez", "ana@acme.com"));

        classified.Rows.Count.ShouldBe(3);
        classified.Rows.ShouldAllBe(row => row.Bucket == UploadRowBucket.DuplicateInFile);
        classified.Rows[0].DuplicateRowNumbers.ShouldBe([2, 3, 4]);
        classified.CollapsedDuplicates.ShouldBe(0);
    }

    /// <summary>
    /// Una fila sin correo no duplica a nadie: las mal formadas salen del grupo antes de agrupar.
    /// </summary>
    [Fact]
    public void Classify_ReturnsMalformedNotDuplicate_WhenMalformedRowsMatch()
    {
        var classified = Classify(
            Row(2, "Ana", "Pérez", email: null),
            Row(3, "Luis", "Gómez", email: null));

        classified.Rows.ShouldAllBe(row => row.Bucket == UploadRowBucket.Malformed);
    }

    /// <summary>
    /// La ecuación de §4: lo clasificado más lo plegado son las filas del archivo. Es lo que
    /// permite creerse el resumen sin volver a abrir la hoja.
    /// </summary>
    [Fact]
    public void Classify_ReturnsRowsPlusCollapsedEqualToTheInput_WhenRowsAreMixed()
    {
        var rows = new[]
        {
            Row(2, "Ana", "Pérez", "ana@acme.com"),
            Row(3, "Ana", "Pérez", "ana@acme.com"),
            Row(4, "Luis", "Gómez", "luis@acme.com"),
            Row(5, "Sara", "Díaz", email: null),
            Row(6, "Iván", "Ruiz", "ivan@acme.com"),
            Row(7, "Iván", "Ruiz Mota", "ivan@acme.com"),
        };

        var classified = UploadRowClassifier.Classify(rows);

        (classified.Rows.Count + classified.CollapsedDuplicates).ShouldBe(rows.Length);
    }

    private static ClassifiedUpload Classify(params ParsedUploadRow[] rows) =>
        UploadRowClassifier.Classify(rows);

    private static ParsedUploadRow Row(int number, string? firstName, string? lastName, string? email) =>
        new(number, firstName, lastName, email, Phone: null, Company: null);
}
