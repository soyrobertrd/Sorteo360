using Sorteo360.Convocatorias.Core.Campaigns;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Campaigns;

/// <summary>
/// Los cuatro motivos de exclusión y, sobre todo, qué pasa cuando el CRM manda uno que este
/// servicio no conoce: es el escenario que decide si la regla vive en una base de código o en dos.
/// </summary>
public sealed class ExclusionReasonTests
{
    [Fact]
    public void All_ReturnsExactlyFourReasons_WhenCalled() =>
        ExclusionReason.All.ShouldBe(
            ["no_email", "opted_out", "do_not_invite", "contact_unavailable"],
            ignoreOrder: true);

    [Theory]
    [InlineData("no_email")]
    [InlineData("opted_out")]
    [InlineData("do_not_invite")]
    public void FromCrm_ReturnsTheReasonAsItCame_WhenTheCrmComputesIt(string fromCrm) =>
        ExclusionReason.FromCrm(fromCrm).ShouldBe(fromCrm);

    /// <summary>
    /// El día que el CRM crezca un quinto motivo, este servicio lo guarda como
    /// <c>contact_unavailable</c> en vez de inventar una regla propia o reventar la selección.
    /// La alternativa —copiar acá el catálogo del CRM— es exactamente cómo la regla acaba
    /// viviendo en dos sitios.
    /// </summary>
    [Theory]
    [InlineData("hard_bounced")]
    [InlineData(null)]
    [InlineData("")]
    public void FromCrm_ReturnsContactUnavailable_WhenTheReasonIsUnknown(string? fromCrm) =>
        ExclusionReason.FromCrm(fromCrm).ShouldBe(ExclusionReason.ContactUnavailable);

    [Fact]
    public void ContactUnavailable_ReturnsOurOwnReasonCode_WhenTheCrmCannotComputeIt() =>
        ExclusionReason.ContactUnavailable.ShouldBe("contact_unavailable");
}
