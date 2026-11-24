using CSharpFunctionalExtensions;
using Sorteo360.Events.Core.Files;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Core.Shared;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Core.Database;

/// <summary>
/// Contrato de persistencia del sorteo de un evento. El servicio carga el sorteo, aplica las
/// reglas y luego invoca la escritura correspondiente.
/// </summary>
public interface IRaffleRepository
{
    /// <summary>
    /// Obtiene el sorteo del evento. <see cref="Maybe{T}.None"/> si el evento no tiene.
    /// </summary>
    Task<Result<Maybe<Raffle>, BaseError>> GetByEventIdAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>
    /// Inserta un sorteo nuevo. Devuelve su identificador. Un segundo sorteo para el mismo evento
    /// choca contra el índice único y vuelve como conflicto.
    /// </summary>
    Task<Result<Guid, BaseError>> AddAsync(Raffle raffle, CancellationToken cancellationToken);

    /// <summary>
    /// Elimina un sorteo en draft con sus rondas, sus premios y sus decisiones manuales, en una
    /// transacción bajo el bloqueo de su fila. Ya bajo el bloqueo comprueba que siga en draft
    /// (<see cref="Raffle.EnsureDraft"/>) y que exista (<c>RAFFLE_NOT_FOUND</c> si otro lo eliminó antes).
    /// Un borrador nunca tiene sorteos de premio, porque reabrir exige que no haya ninguno; si uno
    /// apareciera, su FK frena el borrado.
    /// </summary>
    Task<UnitResult<BaseError>> DeleteDraftAsync(Guid raffleId, CancellationToken cancellationToken);

    /// <summary>
    /// Una página de los sorteos de todos los eventos no eliminados, del evento más reciente al más
    /// viejo, con el nombre y la fecha del evento leídos al consultar.
    /// </summary>
    Task<Result<PagedResult<RaffleListItem>, BaseError>> ListAsync(int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>Las cifras de todos los sorteos de eventos no eliminados.</summary>
    Task<Result<RaffleTotals, BaseError>> GetTotalsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Reemplaza los campos editables del sorteo. Una escritura concurrente sobre la misma fila
    /// choca contra su token xmin y vuelve como conflicto.
    /// </summary>
    Task<UnitResult<BaseError>> UpdateAsync(Raffle raffle, CancellationToken cancellationToken);

    /// <summary>
    /// Obtiene el sorteo del evento con sus rondas, los premios de cada una y todos sus sorteos de
    /// premio. <see cref="Maybe{T}.None"/> si el evento no tiene sorteo.
    /// </summary>
    Task<Result<Maybe<RaffleWithRounds>, BaseError>> GetWithRoundsByEventIdAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>
    /// Inserta una ronda al final del sorteo y devuelve su identificador. El número que se guarda
    /// lo asigna esta escritura —el mayor más uno— bajo el bloqueo de la fila del sorteo, así que
    /// dos altas simultáneas nunca toman el mismo; el que trae la ronda no se lee.
    /// <c>RAFFLE_NOT_FOUND</c> si el borrador se eliminó mientras tanto.
    /// </summary>
    Task<Result<Guid, BaseError>> AddRoundAsync(RaffleRound round, CancellationToken cancellationToken);

    /// <summary>
    /// Reemplaza el nombre y la descripción de la ronda, en una transacción bajo el bloqueo de la fila
    /// del sorteo (ADR-0028): relee qué premios tiene la ronda y todos los sorteos del sorteo, y se los
    /// pasa a <paramref name="guard"/>. Si falla no escribe nada y vuelve su error. <c>false</c> si la
    /// ronda ya no existe.
    /// </summary>
    Task<Result<bool, BaseError>> UpdateRoundAsync(
        RaffleRound round,
        Func<IReadOnlyCollection<Guid>, IReadOnlyList<PrizeDraw>, UnitResult<BaseError>> guard,
        CancellationToken cancellationToken);

    /// <summary>
    /// Elimina la ronda y baja un puesto las que venían detrás, en una transacción bajo el bloqueo
    /// de la fila del sorteo. <c>false</c> si ya no existe. Un premio que entró en la ronda después
    /// de que el servicio la viera vacía choca contra la FK y vuelve como conflicto.
    /// </summary>
    Task<Result<bool, BaseError>> DeleteRoundAsync(Guid raffleId, Guid roundId, CancellationToken cancellationToken);

    /// <summary>
    /// Inserta los premios en una sola escritura, o entran todos o ninguno, bajo el bloqueo de la fila
    /// del sorteo: <c>RAFFLE_NOT_FOUND</c> si el borrador se eliminó mientras tanto.
    /// </summary>
    Task<UnitResult<BaseError>> AddPrizesAsync(Guid raffleId, IReadOnlyList<Prize> prizes, CancellationToken cancellationToken);

    /// <summary>
    /// Inserta rondas faltantes y premios en bloque en una sola transacción (todo o nada), bajo el
    /// bloqueo de la fila del sorteo y verificando que siga en borrador (A30).
    /// </summary>
    Task<UnitResult<BaseError>> ImportPrizesAsync(
        Guid raffleId,
        IReadOnlyList<RaffleRound> newRounds,
        IReadOnlyList<Prize> newPrizes,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reemplaza los campos editables del premio, su ronda incluida, en una transacción bajo el
    /// bloqueo de la fila del sorteo (ADR-0028): relee todos los sorteos del sorteo y se los pasa a
    /// <paramref name="guard"/>. Si falla no escribe nada y vuelve su error. <c>false</c> si el premio
    /// ya no existe.
    /// <para>
    /// El logo del patrocinador no viaja en <paramref name="prize"/>: se conserva si la empresa
    /// patrocinadora sigue siendo la misma y se desenlaza si cambia o se quita, en la misma sentencia.
    /// </para>
    /// </summary>
    Task<Result<bool, BaseError>> UpdatePrizeAsync(
        Guid raffleId,
        Prize prize,
        Func<IReadOnlyList<PrizeDraw>, UnitResult<BaseError>> guard,
        CancellationToken cancellationToken);

    /// <summary>
    /// Elimina el premio, con el mismo bloqueo y la misma guarda que su edición. <c>false</c> si ya
    /// no existe.
    /// </summary>
    Task<Result<bool, BaseError>> DeletePrizeAsync(
        Guid raffleId,
        Guid prizeId,
        Func<IReadOnlyList<PrizeDraw>, UnitResult<BaseError>> guard,
        CancellationToken cancellationToken);

    /// <summary>
    /// Escribe la ficha del archivo y repunta el logo del patrocinador del premio a ella, en una
    /// transacción bajo el bloqueo de la fila del sorteo: relee el premio y todos los sorteos y se los
    /// pasa a <paramref name="guard"/>. Si falla no escribe nada y vuelve su error;
    /// <c>PRIZE_NOT_FOUND</c> si el premio ya no existe.
    /// </summary>
    Task<UnitResult<BaseError>> SetPrizeSponsorLogoAsync(
        Guid raffleId,
        Guid prizeId,
        StoredFile logo,
        Func<Prize, IReadOnlyList<PrizeDraw>, UnitResult<BaseError>> guard,
        CancellationToken cancellationToken);

    /// <summary>
    /// Vacía el logo del patrocinador del premio, con el mismo bloqueo y la misma guarda que su
    /// edición. <c>false</c> si el premio ya no existe.
    /// </summary>
    Task<Result<bool, BaseError>> RemovePrizeSponsorLogoAsync(
        Guid raffleId,
        Guid prizeId,
        Guid removedById,
        DateTimeOffset removedAt,
        Func<IReadOnlyList<PrizeDraw>, UnitResult<BaseError>> guard,
        CancellationToken cancellationToken);

    /// <summary>Las decisiones manuales del sorteo, sin orden: el cálculo las busca por correo.</summary>
    Task<Result<IReadOnlyList<AttendeeOverride>, BaseError>> ListOverridesAsync(Guid raffleId, CancellationToken cancellationToken);

    /// <summary>
    /// Escribe la decisión manual sobre ese correo, o reemplaza la que hubiera —decisión, nota,
    /// quién y cuándo— en una sola sentencia, así que dos escrituras simultáneas no chocan. Bajo el
    /// bloqueo de la fila del sorteo, y solo si sigue en draft (<c>RAFFLE_NOT_DRAFT</c> si no): una
    /// decisión nunca entra después del congelado para el que se tomó (spec 01 §9.1).
    /// </summary>
    Task<UnitResult<BaseError>> UpsertOverrideAsync(Guid raffleId, AttendeeOverride attendeeOverride, CancellationToken cancellationToken);

    /// <summary>
    /// Borra la decisión manual sobre ese correo. No hacer nada si no había es un éxito. Con el mismo
    /// bloqueo y la misma comprobación de draft que la escritura.
    /// </summary>
    Task<UnitResult<BaseError>> DeleteOverrideAsync(Guid raffleId, string emailNormalized, CancellationToken cancellationToken);

    /// <summary>
    /// Arranca el sorteo en una transacción bajo el bloqueo de su fila: relee el sorteo y sus
    /// decisiones manuales, le pasa los dos a <paramref name="freeze"/> —que es quien decide—, y
    /// escribe lo que devuelva: el sorteo en curso y todas las filas de la puerta. Si
    /// <paramref name="freeze"/> falla no se escribe nada y vuelve su error.
    /// <para>
    /// Dos arranques simultáneos se ordenan en el bloqueo, así que el segundo relee un sorteo que ya
    /// no está en draft y nunca escribe un segundo congelado.
    /// </para>
    /// </summary>
    Task<UnitResult<BaseError>> StartAsync(
        Guid raffleId,
        Func<Raffle, IReadOnlyList<AttendeeOverride>, Result<RaffleFreeze, BaseError>> freeze,
        CancellationToken cancellationToken);

    /// <summary>Las filas que escribió el arranque, sin orden.</summary>
    Task<Result<IReadOnlyList<RaffleAttendee>, BaseError>> ListFrozenAttendeesAsync(Guid raffleId, CancellationToken cancellationToken);

    /// <summary>
    /// Un premio del sorteo con sus sorteos (del más nuevo al más viejo, cada uno con sus entregas) y
    /// el logo de su patrocinador, sin leer el resto de los premios. <c>None</c> si el premio no es de
    /// una ronda de este sorteo. Es lo que contestan el giro, la anulación y la entrega (A31).
    /// </summary>
    Task<Result<Maybe<PrizeWithDraws>, BaseError>> GetPrizeWithDrawsAsync(Guid raffleId, Guid prizeId, CancellationToken cancellationToken);

    /// <summary>
    /// Sortea un premio en una transacción bajo el bloqueo de la fila del sorteo (ADR-0028): relee el
    /// sorteo, el premio, los asistentes congelados y todos los sorteos de premio, le pasa los cuatro
    /// a <paramref name="decide"/> —que es quien decide— y escribe el sorteo que devuelva. Si
    /// <paramref name="decide"/> falla no se escribe nada y vuelve su error.
    /// <para>
    /// Antes de escribir comprueba que el código de retiro no esté usado, y genera otro si lo está,
    /// hasta tres intentos; un tercer choque es <c>DB_CONFLICT</c>. Nunca escribe la fila del sorteo,
    /// así que su <c>xmin</c> no cambia y un PATCH en curso no choca por un sorteo de premio.
    /// <c>PRIZE_NOT_FOUND</c> si el premio se fue antes del bloqueo.
    /// </para>
    /// </summary>
    Task<Result<PrizeDraw, BaseError>> DrawAsync(
        Guid raffleId,
        Guid prizeId,
        Func<Raffle, Prize, IReadOnlyList<RaffleAttendee>, IReadOnlyList<PrizeDraw>, Result<PrizeDraw, BaseError>> decide,
        CancellationToken cancellationToken);

    /// <summary>
    /// Sortea hasta 10 premios en una sola transacción y una sola lectura bajo el bloqueo del sorteo (A33/R13).
    /// Si el bombo se agota a mitad del lote, persiste los sorteados hasta ese punto y devuelve el error
    /// <see cref="PoolExhaustedError"/> junto con los sorteos completados.
    /// </summary>
    Task<Result<DrawBatchOutcome, BaseError>> DrawBatchAsync(
        Guid raffleId,
        IReadOnlyList<Guid> prizeIds,
        Func<Raffle, Prize, IReadOnlyList<RaffleAttendee>, IReadOnlyList<PrizeDraw>, Result<PrizeDraw, BaseError>> decide,
        CancellationToken cancellationToken);

    /// <summary>
    /// Asigna o quita (<c>imageFileId = null</c>) la foto de un grupo de premios del sorteo (A34).
    /// Permitido en borrador o en curso; devuelve <c>false</c> si alguno de los premios no pertenece al sorteo.
    /// </summary>
    Task<Result<bool, BaseError>> SetPrizesImageAsync(
        Guid raffleId,
        IReadOnlyList<Guid> prizeIds,
        StoredFile? imageFile,
        Guid updatedById,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken);

    /// <summary>
    /// Anula el ganador vivo de un premio en una transacción bajo el bloqueo de la fila del sorteo
    /// (ADR-0028): relee el sorteo, el premio y todos los sorteos de premio, le pasa los tres a
    /// <paramref name="decide"/> y escribe en ese sorteo las cuatro columnas de la anulación que
    /// devuelva. Si <paramref name="decide"/> falla no se escribe nada y vuelve su error. Nunca escribe
    /// la fila del sorteo. <c>PRIZE_NOT_FOUND</c> si el premio se fue antes del bloqueo.
    /// </summary>
    Task<Result<PrizeDraw, BaseError>> VoidAsync(
        Guid raffleId,
        Guid prizeId,
        Func<Raffle, Prize, IReadOnlyList<PrizeDraw>, Result<PrizeDraw, BaseError>> decide,
        CancellationToken cancellationToken);

    /// <summary>
    /// Entrega un premio en una transacción bajo el bloqueo de la fila del sorteo (ADR-0028): relee el
    /// sorteo, el premio y todos los sorteos de premio con sus entregas, le pasa los tres a
    /// <paramref name="decide"/> y escribe en una sola vez el archivo de la firma y la entrega que
    /// devuelva. Si <paramref name="decide"/> falla no se escribe nada y vuelve su error; los bytes ya
    /// guardados los descarta quien llama. Nunca escribe la fila del sorteo. <c>PRIZE_NOT_FOUND</c> si
    /// el premio se fue antes del bloqueo.
    /// </summary>
    Task<Result<PrizeHandover, BaseError>> HandOverAsync(
        Guid raffleId,
        Guid prizeId,
        StoredFile signature,
        Func<Raffle, Prize, IReadOnlyList<PrizeDraw>, Result<PrizeHandover, BaseError>> decide,
        CancellationToken cancellationToken);

    /// <summary>
    /// Revierte la entrega viva de un premio con el mismo bloqueo y la misma relectura que
    /// <see cref="HandOverAsync"/>, y escribe en esa entrega las tres columnas de la reversión que
    /// devuelva <paramref name="decide"/>. La fila y su firma quedan.
    /// </summary>
    Task<Result<PrizeHandover, BaseError>> ReverseHandoverAsync(
        Guid raffleId,
        Guid prizeId,
        Func<Raffle, Prize, IReadOnlyList<PrizeDraw>, Result<PrizeHandover, BaseError>> decide,
        CancellationToken cancellationToken);

    /// <summary>
    /// El sorteo de premio con ese código canónico, en cualquier sorteo y vivo o anulado, con sus
    /// entregas y de qué evento, sorteo y premio es. Igualdad exacta sobre el índice único; sin
    /// bloqueo y sin escribir nada. <see cref="Maybe{T}.None"/> si ningún sorteo lo tiene.
    /// </summary>
    Task<Result<Maybe<PrizeDrawLookup>, BaseError>> FindDrawByCodeAsync(string redemptionCode, CancellationToken cancellationToken);

    /// <summary>
    /// Completa el sorteo en una transacción bajo el bloqueo de su fila, tomado antes de la primera
    /// lectura y soltado al confirmar (spec 03 §4.4): relee el sorteo y le aplica
    /// <see cref="Raffle.Complete"/>; si pasa, lee todo lo que el acta afirma —rondas, premios,
    /// sorteos, la lista congelada y el evento— y se lo pasa a <paramref name="produce"/> con la
    /// versión 1 y nada que reemplazar; este arma y guarda el PDF. Con el archivo que devuelva escribe
    /// <c>stored_file</c>, el acta versión 1 y el sorteo completado, y confirma.
    /// <para>
    /// Si algo falla no se escribe nada y vuelve el error. Los bytes que <paramref name="produce"/>
    /// ya guardó los descarta quien llama, que es quien sabe dónde quedaron. Nunca mira el estado del
    /// evento (spec 00 §5).
    /// </para>
    /// </summary>
    Task<UnitResult<BaseError>> CompleteAsync(
        Guid raffleId,
        Guid completedById,
        DateTimeOffset now,
        Func<NotarialRecordFacts, int, NotarialRecordReplaces?, Task<Result<StoredFile, BaseError>>> produce,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reemite el acta en una transacción bajo el bloqueo de la fila del sorteo, como
    /// <see cref="CompleteAsync"/> (spec 03 §4.5): relee el sorteo y le aplica
    /// <see cref="Raffle.Reissue"/>; si pasa, lee la última versión del acta y todo lo que el acta
    /// afirma, y se lo pasa a <paramref name="produce"/> con la versión siguiente y la que reemplaza.
    /// Con el archivo que devuelva escribe <c>stored_file</c>, la nueva versión y las autoridades
    /// corregidas, y confirma. Leer la versión bajo el bloqueo es lo que pone en fila dos reemisiones
    /// simultáneas; el índice único sobre <c>(raffle_id, version)</c> es el respaldo.
    /// <para>
    /// Si algo falla no se escribe nada, la versión vigente sigue siéndolo y vuelve el error. Los bytes
    /// ya guardados los descarta quien llama. Nunca mira el estado del evento (spec 00 §5).
    /// </para>
    /// </summary>
    Task<UnitResult<BaseError>> ReissueAsync(
        Guid raffleId,
        RaffleAuthorities authorities,
        Guid reissuedById,
        DateTimeOffset now,
        Func<NotarialRecordFacts, int, NotarialRecordReplaces?, Task<Result<StoredFile, BaseError>>> produce,
        CancellationToken cancellationToken);

    /// <summary>
    /// Registra en el sorteo de premio cómo terminó el correo al ganador: una sola fila, sin bloqueo,
    /// porque el sorteo ya está completado y nadie más escribe estas columnas (spec 03 §5.2).
    /// <c>false</c> si el sorteo ya no existe.
    /// </summary>
    Task<Result<bool, BaseError>> UpdateWinnerNotificationAsync(
        Guid drawId,
        string winnerNotificationStatus,
        string? winnerNotificationId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Devuelve el sorteo a draft en una transacción bajo el bloqueo de su fila (ADR-0028): relee el
    /// sorteo y todos sus sorteos de premio, se los pasa a <paramref name="decide"/> y escribe el
    /// sorteo que devuelva, borrando las filas que congeló el arranque. Las decisiones manuales no se
    /// tocan. Si <paramref name="decide"/> falla no se escribe nada y vuelve su error.
    /// </summary>
    Task<UnitResult<BaseError>> ReopenAsync(
        Guid raffleId,
        Func<Raffle, IReadOnlyList<PrizeDraw>, Result<Raffle, BaseError>> decide,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reactiva a un participante congelado que tenga al menos una anulación activa para que vuelva a
    /// participar en los sorteos siguientes, bajo el bloqueo del sorteo.
    /// </summary>
    Task<UnitResult<BaseError>> ReinstateAttendeeAsync(
        Guid raffleId,
        Guid attendeeId,
        Guid reinstatedById,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}
