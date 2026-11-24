namespace Sorteo360.Convocatorias.Web.Validators;

/// <summary>
/// Textos de validación que devuelve el servicio. Van en español porque los lee una persona; el
/// nombre de cada miembro y el código que acompaña al fallo se quedan en inglés.
/// </summary>
internal static class ValidationMessages
{
    public const string Required = "El campo [{PropertyPath}] es obligatorio.";
    public const string MaximumLength = "El campo [{PropertyPath}] no debe superar los {MaxLength} caracteres.";
    public const string InvalidPhone = "El campo [{PropertyPath}] debe tener entre 7 y 20 caracteres, solo dígitos, espacios y + - ( ).";
    public const string InvalidLanguage = "El campo [{PropertyPath}] debe ser uno de los idiomas admitidos: es, en, fr, de, pt, it.";
    public const string NotNegative = "El campo [{PropertyPath}] no puede ser negativo.";
    public const string EmptyIdentifier = "El campo [{PropertyPath}] debe contener un identificador no vacío.";
    public const string MaximumItems = "El campo [{PropertyPath}] no debe contener más de {MaxItems} elementos.";
    public const string NotWithContactIds = "El campo [{PropertyPath}] no se admite junto con [ContactIds].";
    public const string NotWithContactListId = "El campo [{PropertyPath}] no se admite junto con [ContactListId].";
    public const string InvalidEmail = "El campo [{PropertyPath}] no tiene forma de dirección de correo.";
    public const string InvalidWebsite = "El campo [{PropertyPath}] no tiene forma de sitio web.";
    public const string InvalidCategory = "El campo [{PropertyPath}] no es una categoría de proveedor admitida.";
    public const string DuplicateValues = "El campo [{PropertyPath}] no puede repetir valores.";
    public const string GreaterThanZero = "El campo [{PropertyPath}] debe ser mayor que cero.";
    public const string CapacityOnlyForVenue = "El campo [{PropertyPath}] solo aplica a proveedores de categoría Venue.";
    public const string InvalidBankCountry = "El campo [{PropertyPath}] debe ser un código ISO 3166 de dos letras.";
    public const string InvalidCurrency = "El campo [{PropertyPath}] debe ser USD, EUR o DOP.";
    public const string InvalidSwiftBic = "El campo [{PropertyPath}] debe tener 8 u 11 letras o números.";
    public const string AbaRequired = "El campo [{PropertyPath}] es obligatorio para un banco de Estados Unidos.";
    public const string InvalidAba = "El campo [{PropertyPath}] debe tener 9 dígitos.";
    public const string InvalidIban = "El campo [{PropertyPath}] no es un IBAN válido: su dígito de control no cuadra.";
    public const string AccountNumberMaximumLength = "El campo [{PropertyPath}] no debe superar los 34 caracteres, sin contar los espacios.";
}
