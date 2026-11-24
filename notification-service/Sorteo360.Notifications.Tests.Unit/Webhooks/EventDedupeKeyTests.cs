using Sorteo360.Notifications.Core.Webhooks;
using Shouldly;

namespace Sorteo360.Notifications.Tests.Unit.Webhooks;

/// <summary>
/// La clave de reserva: lo que deduplica un evento que llegó sin <c>sg_event_id</c>, que es
/// exactamente el caso de los rebotes asíncronos.
/// </summary>
public sealed class EventDedupeKeyTests
{
    private const string Email = "maria@sorteo360.local";
    private const long Timestamp = 1757800000;

    [Fact]
    public void Composite_ReturnsTheSameKey_WhenTheFourFieldsAreTheSame() =>
        EventDedupeKey.Composite("bounce", Email, Timestamp, "msg-1")
            .ShouldBe(EventDedupeKey.Composite("bounce", Email, Timestamp, "msg-1"));

    [Theory]
    [InlineData("deferred", Email, Timestamp, "msg-1")]
    [InlineData("bounce", "jose@sorteo360.local", Timestamp, "msg-1")]
    [InlineData("bounce", Email, Timestamp + 1, "msg-1")]
    [InlineData("bounce", Email, Timestamp, "msg-2")]
    public void Composite_ReturnsAnotherKey_WhenAnyOfTheFourChanges(
        string eventType, string email, long timestamp, string? sgMessageId) =>
        EventDedupeKey.Composite(eventType, email, timestamp, sgMessageId)
            .ShouldNotBe(EventDedupeKey.Composite("bounce", Email, Timestamp, "msg-1"));

    [Fact]
    public void Composite_ReturnsAnotherKey_WhenTheMessageIdIsMissing() =>
        EventDedupeKey.Composite("bounce", Email, Timestamp, sgMessageId: null)
            .ShouldNotBe(EventDedupeKey.Composite("bounce", Email, Timestamp, "msg-1"));

    [Fact]
    public void Composite_ReturnsAnotherKey_WhenAValueSlidesBetweenFields() =>
        // Sin separador, "ab" + "c" y "a" + "bc" darían el mismo hash, y dos eventos distintos se
        // tratarían como uno.
        EventDedupeKey.Composite("bounce", "ab", Timestamp, "c")
            .ShouldNotBe(EventDedupeKey.Composite("bounce", "a", Timestamp, "bc"));

    [Fact]
    public void DedupeKey_ReturnsNull_WhenTheEventHasAProviderId()
    {
        var withId = new SendGridEvent("bounce", Email, Timestamp, "sg-1", "msg-1", null, "{}");

        // La columna queda vacía a propósito: ahí el índice único de sg_event_id ya hace el
        // trabajo, y guardar el hash además daría dos formas de decir lo mismo.
        withId.DedupeKey.ShouldBeNull();
    }

    [Fact]
    public void DedupeKey_ReturnsTheComposite_WhenTheEventHasNoProviderId()
    {
        var withoutId = new SendGridEvent("bounce", Email, Timestamp, null, "msg-1", null, "{}");

        withoutId.DedupeKey.ShouldBe(EventDedupeKey.Composite("bounce", Email, Timestamp, "msg-1"));
    }
}
