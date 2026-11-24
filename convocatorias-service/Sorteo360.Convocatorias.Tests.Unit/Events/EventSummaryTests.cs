using Sorteo360.Convocatorias.Core.Events;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Events;

/// <summary>
/// Cuándo un evento sigue admitiendo inscripciones (spec <c>06</c> §2.1 y §4.6). Son dos
/// condiciones y hacen falta las dos.
/// </summary>
public sealed class EventSummaryTests
{
    private static readonly DateOnly Today = new(2026, 9, 21);

    [Theory]
    [InlineData(EventStatus.Planning)]
    [InlineData(EventStatus.InProgress)]
    public void AcceptsRegistrationsOn_ReturnsTrue_WhenTheEventIsStillAhead(string status)
    {
        Summary(status, endsInDays: 10).AcceptsRegistrationsOn(Today).ShouldBeTrue();
    }

    /// <summary>Cancelado no admite a nadie, y es 404 en todo el portal.</summary>
    [Fact]
    public void AcceptsRegistrationsOn_ReturnsFalse_WhenTheEventWasCancelled()
    {
        Summary(EventStatus.Cancelled, endsInDays: 10).AcceptsRegistrationsOn(Today).ShouldBeFalse();
    }

    /// <summary>
    /// Cerrado tampoco, <b>y todavía le quedan días por delante</b>: es el caso que §4.6 pide
    /// probar así, para que lo que rechace sea el estado y no el calendario.
    /// </summary>
    [Fact]
    public void AcceptsRegistrationsOn_ReturnsFalse_WhenTheEventWasClosedBeforeItsEndDate()
    {
        Summary(EventStatus.Closed, endsInDays: 10).AcceptsRegistrationsOn(Today).ShouldBeFalse();
    }

    /// <summary>
    /// Y un evento vivo cuya última fecha ya pasó tampoco: un token no caduca por su cuenta,
    /// pero deja de valer cuando ya no hay a qué ir.
    /// </summary>
    [Fact]
    public void AcceptsRegistrationsOn_ReturnsFalse_WhenTheEndDateIsBehindUs()
    {
        Summary(EventStatus.InProgress, endsInDays: -1).AcceptsRegistrationsOn(Today).ShouldBeFalse();
    }

    /// <summary>El último día cuenta entero: la puerta se cierra al día siguiente.</summary>
    [Fact]
    public void AcceptsRegistrationsOn_ReturnsTrue_WhenTodayIsTheLastDay()
    {
        Summary(EventStatus.InProgress, endsInDays: 0).AcceptsRegistrationsOn(Today).ShouldBeTrue();
    }

    /// <summary>
    /// La visibilidad no entra en esto: a un evento privado se inscribe quien recibió su
    /// invitación, y quién puede entrar lo decide el token, no esta regla.
    /// </summary>
    [Fact]
    public void AcceptsRegistrationsOn_ReturnsTrue_WhenTheEventIsPrivate()
    {
        var hidden = Summary(EventStatus.InProgress, endsInDays: 10) with
        {
            Visibility = EventVisibility.Private
        };

        hidden.AcceptsRegistrationsOn(Today).ShouldBeTrue();
    }

    private static EventSummary Summary(string status, int endsInDays) => new()
    {
        Id         = Guid.NewGuid(),
        Name       = "Roadshow Madrid 2026",
        Type       = "Roadshow",
        Status     = status,
        Language   = "es",
        Visibility = EventVisibility.Public,
        StartDate  = Today.AddDays(endsInDays - 2),
        EndDate    = Today.AddDays(endsInDays),
    };
}
