namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// Convierte los hechos de un acta en el PDF que se guarda. El dominio no sabe con qué biblioteca
/// ni en qué idioma: la redacción, el formato de las horas y la maquetación son de quien
/// implementa (ADR-0024).
/// </summary>
public interface INotarialRecordRenderer
{
    byte[] Render(NotarialRecordContent content);
}
