using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Web.RaffleEndpoints;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.Raffles;

/// <summary>
/// Lo que el sorteo y su bombo deliberadamente no tienen, afirmado como ausencia: el sorteo no lleva
/// cuerpo, porque nada lo elige el cliente (spec 00); el bombo no expone el correo ni el código de
/// retiro, porque es para un proyector (spec 02 §5); y un sorteo no expone el correo (spec 02 §7.1).
/// </summary>
public sealed class PrizeDrawShapeTests
{
    [Fact]
    public void GetParameters_ReturnsNoBodyParameter_WhenReadingTheDrawHandler()
    {
        var handler = typeof(PrizeDrawEndpoints).GetMethod("DrawPrize", BindingFlags.NonPublic | BindingFlags.Static);

        handler.ShouldNotBeNull();
        handler.GetParameters().ShouldNotContain(p => p.GetCustomAttribute<FromBodyAttribute>() != null);
    }

    [Theory]
    [InlineData(typeof(EligibleParticipantResponse))]
    [InlineData(typeof(EligibleResponse))]
    public void GetProperties_ReturnsNoEmailOrRedemptionCode_WhenReadingAProjectorShape(Type shape)
    {
        shape.GetProperties().ShouldNotContain(p =>
            p.Name.Contains("Email", StringComparison.OrdinalIgnoreCase)
            || p.Name.Contains("Redemption", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Las listas que cruzan sorteos las lee todo el personal (la fallback policy): ni el correo del ganador
    /// ni su código de retiro, que es del correo del ganador (spec 03 §6).
    /// </summary>
    [Theory]
    [InlineData(typeof(RaffleListItemResponse))]
    [InlineData(typeof(RaffleTotalsResponse))]
    public void GetProperties_ReturnsNoEmailOrRedemptionCode_WhenReadingAListShape(Type shape)
    {
        shape.GetProperties().ShouldNotContain(p =>
            p.Name.Contains("Email", StringComparison.OrdinalIgnoreCase)
            || p.Name.Contains("Redemption", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// El premio y sus sorteos llevan el código de retiro desde spec 03 §3.4, y siguen sin el correo
    /// del ganador.
    /// </summary>
    [Theory]
    [InlineData(typeof(PrizeDrawSummary))]
    [InlineData(typeof(PrizeHandoverSummary))]
    [InlineData(typeof(PrizeResponse))]
    public void GetProperties_ReturnsNoEmail_WhenReadingThePrizeShape(Type shape)
    {
        shape.GetProperties().ShouldNotContain(p => p.Name.Contains("Email", StringComparison.OrdinalIgnoreCase));
    }
}
