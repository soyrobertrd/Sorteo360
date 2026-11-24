using System.Reflection;
using System.Text.RegularExpressions;
using Sorteo360.Events.Core.Raffles;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.Raffles;

/// <summary>El código de retiro y el azar del sorteo (spec 03 §2.2, spec 00 §10).</summary>
public sealed class RedemptionCodeTests
{
    [Fact]
    public void Generate_ReturnsRdAndTwoGroupsOfFourCrockfordCharacters_WhenCalledRepeatedly()
    {
        var pattern = new Regex(@"^RD-[0-9A-HJKMNP-TV-Z]{4}-[0-9A-HJKMNP-TV-Z]{4}$");

        var codes = Enumerable.Range(0, 1000).Select(_ => RedemptionCode.Generate()).ToList();

        codes.ShouldAllBe(c => pattern.IsMatch(c));
        codes.Distinct().Count().ShouldBeGreaterThan(990);
    }

    [Fact]
    public void Pattern_ReturnsTheExpressionTheDatabaseChecks_WhenRead()
    {
        RedemptionCode.Pattern.ShouldBe("^RD-[0-9A-HJKMNP-TV-Z]{4}-[0-9A-HJKMNP-TV-Z]{4}$");
    }

    [Theory]
    [InlineData("rd7k3mq9xa", "RD-7K3M-Q9XA")]
    [InlineData("RD 7K3M Q9XA", "RD-7K3M-Q9XA")]
    [InlineData("7K3MQ9XA", "RD-7K3M-Q9XA")]
    [InlineData("RD-7K3M-Q9XA", "RD-7K3M-Q9XA")]
    [InlineData(" 7k3m-q9xa ", "RD-7K3M-Q9XA")]
    [InlineData("rd-lk3o-q9xa", "RD-1K30-Q9XA")]
    [InlineData("RD-IK3O-Q9XA", "RD-1K30-Q9XA")]
    [InlineData("RDRDRDRD", "RD-RDRD-RDRD")]
    public void TryNormalize_ReturnsTheCanonicalCode_WhenTheInputReadsAsOne(string input, string expected)
    {
        RedemptionCode.TryNormalize(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("RD-7K3M-Q9X")]
    [InlineData("7K3MQ9X")]
    [InlineData("RD-7K3M-Q9XAB")]
    [InlineData("7K3MQ9XAB")]
    [InlineData("RD-7K3M-Q9XU")]
    [InlineData("RD-7K3M.Q9XA")]
    [InlineData("RD-7K3M-Q9X%")]
    [InlineData("XX-7K3M-Q9XA")]
    public void TryNormalize_ReturnsNull_WhenTheInputIsNotACode(string input)
    {
        RedemptionCode.TryNormalize(input).ShouldBeNull();
    }

    [Fact]
    public void TryNormalize_ReturnsCodesTheDatabaseAccepts_WhenGivenGeneratedCodesInLowercase()
    {
        var pattern = new Regex(RedemptionCode.Pattern);

        var codes = Enumerable.Range(0, 200).Select(_ => RedemptionCode.Generate()).ToList();

        codes.ShouldAllBe(c => RedemptionCode.TryNormalize(c.ToLowerInvariant()) == c);
        codes.ShouldAllBe(c => pattern.IsMatch(RedemptionCode.TryNormalize(c)!));
    }

    /// <summary>
    /// Ningún tipo del espacio de nombres del sorteo —lambdas y máquinas de estado incluidas— usa
    /// <see cref="Random"/>: se recorre el IL de cada método buscando un token que resuelva a un
    /// miembro suyo.
    /// </summary>
    [Fact]
    public void GetTypes_ReturnsNoRaffleTypeThatReferencesSystemRandom_WhenScanningTheIl()
    {
        var raffleTypes = typeof(Raffle).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(Raffle).Namespace)
            .ToList();

        var offenders = raffleTypes
            .Where(t => Signatures(t).Any(IsRandom) || Methods(t).Any(ReferencesRandom))
            .Select(t => t.FullName)
            .ToList();

        raffleTypes.ShouldNotBeEmpty();
        offenders.ShouldBeEmpty();
    }

    private const BindingFlags Everything =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static bool IsRandom(Type? type) => type is not null && typeof(Random).IsAssignableFrom(type);

    private static IEnumerable<Type> Signatures(Type type) =>
        type.GetFields(Everything).Select(f => f.FieldType)
            .Concat(type.GetProperties(Everything).Select(p => p.PropertyType));

    private static IEnumerable<MethodBase> Methods(Type type) =>
        type.GetMethods(Everything).Cast<MethodBase>().Concat(type.GetConstructors(Everything));

    private static bool ReferencesRandom(MethodBase method)
    {
        var body = method.GetMethodBody();
        if (body is null)
            return false;

        if (body.LocalVariables.Any(l => IsRandom(l.LocalType)))
            return true;

        // Cada token de miembro ocupa cuatro bytes y termina en su tabla: 0x0A MemberRef, 0x06 MethodDef,
        // 0x04 Field. Se prueban todas las ventanas; lo que no resuelve no es un token.
        var il = body.GetILAsByteArray() ?? [];
        for (var i = 0; i + 4 <= il.Length; i++)
        {
            if (il[i + 3] is not (0x0A or 0x06 or 0x04 or 0x2B))
                continue;

            var token = BitConverter.ToInt32(il, i);
            try
            {
                var member = method.Module.ResolveMember(
                    token,
                    method.DeclaringType?.GetGenericArguments(),
                    method.IsGenericMethod ? method.GetGenericArguments() : null);
                if (IsRandom(member?.DeclaringType))
                    return true;
            }
            catch (Exception ex) when (ex is ArgumentException or BadImageFormatException)
            {
            }
        }

        return false;
    }
}
