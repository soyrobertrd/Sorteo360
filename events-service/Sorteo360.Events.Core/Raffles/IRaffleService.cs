using CSharpFunctionalExtensions;
using Sorteo360.Events.Core.DTOs;
using Sorteo360.Events.Core.Files;
using Sorteo360.Events.Core.Shared;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Core.Raffles;

public interface IRaffleService
{
    /// <summary>
    /// Crea el sorteo del evento en draft, con los valores por omisión. Devuelve su identificador.
    /// </summary>
    Task<Result<Guid, BaseError>> CreateAsync(Guid eventId, Guid createdById, CancellationToken cancellationToken);

    /// <summary>
    /// El sorteo del evento con sus rondas y premios, en cualquier estado del evento, y si está
    /// listo para arrancar. Un borrador lee la puerta para saberlo, por la copia reciente; nunca
    /// falla por ella.
    /// </summary>
    Task<Result<RaffleDetail, BaseError>> GetAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>
    /// Elimina el sorteo del evento mientras siga en draft, con sus rondas, premios y decisiones
    /// manuales. No sobre un evento cerrado o cancelado. Después el evento admite un sorteo nuevo.
    /// </summary>
    Task<UnitResult<BaseError>> DeleteAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>Una página de los sorteos de todos los eventos, del evento más reciente al más viejo.</summary>
    Task<Result<PagedResult<RaffleListItem>, BaseError>> ListAsync(int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>Las cifras de todos los sorteos juntos.</summary>
    Task<Result<RaffleTotals, BaseError>> GetTotalsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// La lista de la puerta clasificada. En draft, calculada en cada lectura sobre la copia reciente
    /// y sin escribir nada, <c>ATTENDANCE_TOO_MANY_ROWS</c> si la puerta pasa del techo; después del
    /// arranque, la lista congelada, sin consultar nada más.
    /// </summary>
    Task<Result<RaffleAttendees, BaseError>> GetAttendeesAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>
    /// Arranca el sorteo: corre el pipeline una vez más con lecturas en vivo y congela una fila por
    /// cada persona en la puerta, calificada o no (spec 01 §4). Devuelve el sorteo como lo devuelve
    /// <see cref="GetAsync"/>. Si algo falla no escribe nada.
    /// </summary>
    Task<Result<RaffleDetail, BaseError>> StartAsync(Guid eventId, Guid startedById, CancellationToken cancellationToken);

    /// <summary>
    /// Devuelve a draft un sorteo en curso que todavía no sorteó nada, y descarta su bombo congelado;
    /// las decisiones manuales quedan (spec 02 §6). Devuelve el sorteo como lo devuelve
    /// <see cref="GetAsync"/>. No sobre un evento cerrado o cancelado.
    /// </summary>
    Task<Result<RaffleDetail, BaseError>> ReopenAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>
    /// Reemplaza la representación editable completa del sorteo (ADR-0009). No sobre un evento
    /// cerrado o cancelado, ni sobre un sorteo completado.
    /// </summary>
    Task<UnitResult<BaseError>> UpdateAsync(UpdateRaffleDto dto, CancellationToken cancellationToken);

    /// <summary>
    /// Reemplaza los firmantes que el certificado de premio propone por omisión, en cualquier estado
    /// del sorteo. No sobre un evento cerrado o cancelado.
    /// </summary>
    Task<UnitResult<BaseError>> SetCertificateSignersAsync(
        Guid eventId, CertificateSigners signers, Guid changedById, CancellationToken cancellationToken);

    /// <summary>
    /// Reemplaza los campos visibles del participante del sorteo mientras esté en borrador (§10).
    /// </summary>
    Task<Result<RaffleDetail, BaseError>> SetDisplayFieldsAsync(
        Guid eventId, RaffleDisplayFields displayFields, Guid changedById, CancellationToken cancellationToken);

    /// <summary>
    /// Completa el sorteo y genera, guarda y enlaza su acta versión 1, todo o nada (spec 03 §4.4).
    /// Devuelve el sorteo como lo lee <see cref="GetAsync"/>. No manda los correos de los ganadores:
    /// eso es <see cref="NotifyWinnersAsync"/>, que quien llama corre después, fuera de la petición.
    /// </summary>
    Task<Result<RaffleDetail, BaseError>> CompleteAsync(Guid eventId, Guid completedById, CancellationToken cancellationToken);

    /// <summary>
    /// Un correo por sorteo de premio vivo de un sorteo completado, y su desenlace guardado en cada
    /// sorteo (spec 03 §5.1). Con 10.000 ganadores tarda minutos, así que corre en segundo plano
    /// después de completar (A31). Nunca falla: lo que no salió queda con su estado o en nulo.
    /// </summary>
    Task NotifyWinnersAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>
    /// Corrige las seis autoridades de un sorteo completado y genera, guarda y enlaza la versión
    /// siguiente del acta, todo o nada (spec 03 §4.5). Devuelve el sorteo como lo lee
    /// <see cref="GetAsync"/>.
    /// </summary>
    Task<Result<RaffleDetail, BaseError>> ReissueAsync(
        Guid eventId, RaffleAuthorities authorities, Guid reissuedById, CancellationToken cancellationToken);

    /// <summary>
    /// Agrega una ronda al final del sorteo, con el siguiente número. Devuelve su identificador.
    /// </summary>
    Task<Result<Guid, BaseError>> CreateRoundAsync(CreateRaffleRoundDto dto, CancellationToken cancellationToken);

    /// <summary>Reemplaza el nombre y la descripción de la ronda (ADR-0009).</summary>
    Task<UnitResult<BaseError>> UpdateRoundAsync(UpdateRaffleRoundDto dto, CancellationToken cancellationToken);

    /// <summary>
    /// Elimina una ronda vacía y renumera las siguientes para que sigan de uno a n sin huecos.
    /// </summary>
    Task<UnitResult<BaseError>> DeleteRoundAsync(Guid eventId, Guid roundId, CancellationToken cancellationToken);

    /// <summary>
    /// Crea <c>Quantity</c> premios idénticos en la ronda y los devuelve en el orden en que se
    /// crearon.
    /// </summary>
    Task<Result<IReadOnlyList<Prize>, BaseError>> CreatePrizesAsync(CreatePrizesDto dto, CancellationToken cancellationToken);

    /// <summary>
    /// Importa premios en bloque (hasta 10.000) creando las rondas que falten en una sola
    /// transacción todo o nada, solo con el sorteo en borrador (A30).
    /// </summary>
    Task<Result<ImportPrizesResult, BaseError>> ImportPrizesAsync(ImportPrizesDto dto, CancellationToken cancellationToken);

    /// <summary>
    /// Reemplaza el premio entero (ADR-0009), y puede moverlo a otra ronda del mismo sorteo.
    /// </summary>
    Task<UnitResult<BaseError>> UpdatePrizeAsync(UpdatePrizeDto dto, CancellationToken cancellationToken);

    /// <summary>Elimina el premio.</summary>
    Task<UnitResult<BaseError>> DeletePrizeAsync(Guid eventId, Guid prizeId, CancellationToken cancellationToken);

    /// <summary>
    /// Sube el logo del patrocinador del premio y reemplaza el que hubiera, que queda desenlazado con
    /// sus bytes intactos (spec 00 §8). Todo lo que puede rechazarla se comprueba antes de escribir un
    /// byte: el evento y el sorteo, el premio, que no tenga sorteos, que tenga patrocinador y que sea
    /// una imagen. Devuelve la ficha del archivo guardado.
    /// </summary>
    Task<Result<StoredFile, BaseError>> SetPrizeSponsorLogoAsync(SetPrizeSponsorLogoDto dto, CancellationToken cancellationToken);

    /// <summary>
    /// Desenlaza el logo del patrocinador del premio, tenga o no uno. No borra el archivo ni sus bytes.
    /// </summary>
    Task<UnitResult<BaseError>> RemovePrizeSponsorLogoAsync(
        Guid eventId,
        Guid prizeId,
        Guid removedById,
        CancellationToken cancellationToken);

    /// <summary>
    /// Sube una foto de premio y la enlaza a todos los <paramref name="prizeIds"/> del sorteo (A34).
    /// Permitido en borrador o en curso.
    /// </summary>
    Task<Result<StoredFile, BaseError>> SetPrizesImageAsync(
        Guid eventId,
        IReadOnlyList<Guid> prizeIds,
        Stream content,
        string fileName,
        string? contentType,
        Guid uploadedById,
        CancellationToken cancellationToken);

    /// <summary>
    /// Quita la foto de los premios indicados (A34). Permitido en borrador o en curso.
    /// </summary>
    Task<UnitResult<BaseError>> RemovePrizesImageAsync(
        Guid eventId,
        IReadOnlyList<Guid> prizeIds,
        Guid removedById,
        CancellationToken cancellationToken);

    /// <summary>
    /// Escribe la decisión manual sobre un asistente, o reemplaza la que hubiera (spec 01 §9.1).
    /// Lee la puerta en vivo y no de la copia; una inclusión además le pregunta al CRM, en vivo, por
    /// ese único contacto. No escribe nada si algo falla.
    /// </summary>
    Task<UnitResult<BaseError>> SetOverrideAsync(SetAttendeeOverrideDto dto, CancellationToken cancellationToken);

    /// <summary>
    /// Borra la decisión manual sobre ese correo, exista o no. No consulta la puerta, así que se
    /// puede retirar la de alguien cuyo registro se revirtió.
    /// </summary>
    Task<UnitResult<BaseError>> RemoveOverrideAsync(Guid eventId, string email, CancellationToken cancellationToken);

    /// <summary>
    /// Sortea el premio: bajo el bloqueo del sorteo calcula su bombo, elige a uno con
    /// <c>RandomNumberGenerator</c> y escribe el sorteo con su código de retiro (spec 02 §2). Devuelve
    /// el premio entero, con su estado y sus sorteos. Cualquiera sea el estado del evento.
    /// </summary>
    Task<Result<PrizeWithDraws, BaseError>> DrawAsync(Guid eventId, Guid prizeId, Guid drawnById, CancellationToken cancellationToken);

    /// <summary>
    /// Sortea un lote de hasta 10 premios en una sola transacción (A33). Devuelve los premios
    /// sorteados en orden; si el bombo se agota a mitad, devuelve los sorteados hasta ese momento
    /// junto con el error <see cref="Errors.PoolExhaustedError"/>.
    /// </summary>
    Task<Result<DrawBatchServiceOutcome, BaseError>> DrawBatchAsync(
        Guid eventId,
        IReadOnlyList<Guid> prizeIds,
        Guid drawnById,
        CancellationToken cancellationToken);

    /// <summary>
    /// Anula el ganador vivo del premio con un motivo y una nota opcional, bajo el bloqueo del sorteo
    /// (spec 02 §3). El sorteo anulado queda en la historia del premio, que vuelve a undrawn. Devuelve
    /// el premio entero, como <see cref="DrawAsync"/>. Cualquiera sea el estado del evento.
    /// </summary>
    Task<Result<PrizeWithDraws, BaseError>> VoidAsync(VoidPrizeDrawDto dto, CancellationToken cancellationToken);

    /// <summary>
    /// Entrega el premio a quien lo retira, con su firma (spec 03 §3). Inspecciona la firma y guarda
    /// los bytes antes de bloquear; el estado del premio se decide una sola vez, bajo el bloqueo del
    /// sorteo, y si la entrega no se escribe los bytes se descartan. Admite un sorteo completado y no
    /// mira el estado del evento. Devuelve el premio entero, como <see cref="DrawAsync"/>.
    /// </summary>
    Task<Result<PrizeWithDraws, BaseError>> HandOverAsync(HandOverPrizeDto dto, CancellationToken cancellationToken);

    /// <summary>
    /// Revierte la entrega viva del premio con un motivo, bajo el bloqueo del sorteo (spec 03 §3.3). La
    /// entrega queda en la historia con su firma, y el premio vuelve a drawn. Devuelve el premio
    /// entero, como <see cref="DrawAsync"/>.
    /// </summary>
    Task<Result<PrizeWithDraws, BaseError>> ReverseHandoverAsync(ReversePrizeHandoverDto dto, CancellationToken cancellationToken);

    /// <summary>
    /// El sorteo de premio que nombra un código de retiro escrito por alguien (spec 03 §2.4): se
    /// normaliza y se compara exacto. <c>REDEMPTION_CODE_NOT_FOUND</c> si lo escrito no es un código o
    /// ningún sorteo lo tiene. Un sorteo anulado se responde igual. No entrega nada ni escribe nada.
    /// </summary>
    Task<Result<PrizeDrawLookup, BaseError>> FindDrawByCodeAsync(string code, CancellationToken cancellationToken);

    /// <summary>
    /// Quién puede ganar el premio ahora mismo, calculado con la misma función que el sorteo y sin
    /// bloquear nada (spec 02 §5). Solo con el sorteo en curso y el premio sin ganador vivo.
    /// </summary>
    Task<Result<EligiblePool, BaseError>> GetEligibleAsync(Guid eventId, Guid prizeId, CancellationToken cancellationToken);

    /// <summary>
    /// Reactiva a un participante anulado para que vuelva a entrar en el bombo de los premios
    /// siguientes, solo con el sorteo en curso (<c>RAFFLE_ATTENDEE_NOT_VOIDED</c> si no tiene ninguna
    /// anulación activa).
    /// </summary>
    Task<Result<RaffleDetail, BaseError>> ReinstateAttendeeAsync(
        Guid eventId,
        Guid attendeeId,
        Guid reinstatedById,
        CancellationToken cancellationToken);
}
