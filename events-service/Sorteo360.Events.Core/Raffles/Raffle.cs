using CSharpFunctionalExtensions;
using Sorteo360.Events.Core.DTOs;
using Sorteo360.Events.Core.Errors;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// El sorteo de un evento, a lo sumo uno por evento (lo garantiza el índice único sobre
/// <c>event_id</c>, no el código). No tiene nombre ni fecha propios: los dos son del evento.
/// <para>
/// Además de su estado guarda cómo se muestra en pantalla —que la laptop del operador y el
/// proyector tienen que compartir— y las dos reglas que limitan quién puede ganar dos veces. Las
/// rondas y los premios cuelgan de él como subrecursos con sus propios endpoints, así que no
/// viajan en su PATCH.
/// </para>
/// </summary>
public sealed record Raffle
{
    public Guid Id { get; init; }
    public Guid EventId { get; init; }
    public string State { get; init; }
    public string VisualStyle { get; init; }
    public bool ShowPrizeOnScreen { get; init; }
    public int AnimationSeconds { get; init; }

    /// <summary>
    /// Si el cliente le pide al operador confirmar que el ganador está en la sala. No se hace
    /// cumplir acá: un ausente es una anulación con motivo, que ya produce la línea del acta.
    /// </summary>
    public bool RequiresPresenceConfirmation { get; init; }

    /// <summary>
    /// Si solo entran al bombo quienes hicieron check-in (por omisión <c>true</c>). En <c>false</c>,
    /// al iniciar entra todo el padrón activo haya o no check-in (A35). Solo editable en borrador.
    /// </summary>
    public bool RequiresCheckIn { get; init; } = true;

    public bool OnePrizePerPerson { get; init; }
    public bool OnePrizePerCompany { get; init; }

    /// <summary>
    /// El evento al que pertenece este sorteo junto con otros (sorteos vinculados). Nulo si no se
    /// vinculó. Los sorteos de un mismo grupo (<see cref="GroupEventId"/>) no repiten ganadores: quien
    /// tiene un premio vivo en uno queda fuera del bombo de los demás. Editable salvo completado; rige
    /// desde el próximo giro.
    /// </summary>
    public Guid? LinkedEventId { get; init; }

    /// <summary>
    /// El grupo del sorteo: el evento vinculado o, sin vínculo, su propio evento. Un sorteo sin
    /// vínculo es el ancla de los que se vinculan a su evento.
    /// </summary>
    public Guid GroupEventId => LinkedEventId ?? EventId;

    public string? BoardTitle { get; init; }
    public string? BoardSubtitle { get; init; }
    public string? BoardIdentifier { get; init; }
    public int? BoardBatchSize { get; init; }
    public RaffleAuthorities Authorities { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public Guid CreatedById { get; init; }
    public Guid UpdatedById { get; init; }

    /// <summary>
    /// Cuándo se arrancó, que es también el instante en que se congeló la lista de la puerta (spec 01
    /// §4). Nulo en draft; junto con <see cref="StartedById"/>, o los dos o ninguno.
    /// </summary>
    public DateTimeOffset? StartedAt { get; init; }

    public Guid? StartedById { get; init; }

    /// <summary>
    /// La lista de DO Travel Awards que se aplicó al arrancar. Nulo en draft y también cuando no
    /// había ninguna en vigor, que es lo que el acta tiene que poder decir.
    /// </summary>
    public Guid? AwardsBatchId { get; init; }

    /// <summary>
    /// Dónde y hasta cuándo se retiran los premios, en texto libre. Lo dice el correo al ganador y
    /// se exige al completar; mientras tanto puede faltar (spec 03 §5.3).
    /// </summary>
    public string? PickupInstructions { get; init; }

    /// <summary>
    /// Los firmantes que el certificado de premio propone por omisión. No atestiguan nada, así que
    /// ningún estado del sorteo los congela.
    /// </summary>
    public CertificateSigners CertificateSigners { get; init; } = CertificateSigners.None;

    /// <summary>Campos visibles del participante en pantalla/proyector (§10).</summary>
    public RaffleDisplayFields DisplayFields { get; init; } = RaffleDisplayFields.Default;

    /// <summary>
    /// Cuándo se completó, el mismo instante que dice el acta. Nulo salvo completado; junto con
    /// <see cref="CompletedById"/>, o los dos o ninguno.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; init; }

    public Guid? CompletedById { get; init; }

    /// <summary>
    /// Un sorteo nuevo, en draft y con los valores por omisión: ruleta, premio visible, seis
    /// segundos de animación, sin confirmación de presencia, un premio por persona sí y uno por
    /// empresa no (spec 00 §7), y las autoridades en blanco.
    /// </summary>
    public Raffle(
        Guid id,
        Guid eventId,
        string state,
        string visualStyle,
        bool showPrizeOnScreen,
        int animationSeconds,
        bool requiresPresenceConfirmation,
        bool onePrizePerPerson,
        bool onePrizePerCompany,
        RaffleAuthorities authorities,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        Guid createdById,
        Guid updatedById)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(visualStyle);
        ArgumentNullException.ThrowIfNull(authorities);

        Id = id;
        EventId = eventId;
        State = state;
        VisualStyle = visualStyle;
        ShowPrizeOnScreen = showPrizeOnScreen;
        AnimationSeconds = animationSeconds;
        RequiresPresenceConfirmation = requiresPresenceConfirmation;
        OnePrizePerPerson = onePrizePerPerson;
        OnePrizePerCompany = onePrizePerCompany;
        Authorities = authorities;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        CreatedById = createdById;
        UpdatedById = updatedById;
    }

    /// <summary>
    /// Un sorteo nuevo, en draft y con los valores por omisión: ruleta, premio visible, seis
    /// segundos de animación, sin confirmación de presencia, un premio por persona sí y uno por
    /// empresa no (spec 00 §7), y las autoridades en blanco.
    /// </summary>
    public static Raffle CreateDraft(Guid eventId, Guid createdById, DateTimeOffset now) => new(
        id: Guid.NewGuid(),
        eventId: eventId,
        state: RaffleState.Draft,
        visualStyle: RaffleVisualStyle.Roulette,
        showPrizeOnScreen: true,
        animationSeconds: 6,
        requiresPresenceConfirmation: false,
        onePrizePerPerson: true,
        onePrizePerCompany: false,
        authorities: RaffleAuthorities.Blank,
        createdAt: now,
        updatedAt: now,
        createdById: createdById,
        updatedById: createdById);

    /// <summary>
    /// Reemplaza la representación editable entera (ADR-0009): lo omitido llega en blanco y queda
    /// en blanco. Se permite en draft y en in_progress; un sorteo completado ya no se toca. En curso
    /// también cambian las dos reglas, y el cambio rige desde el próximo sorteo: cada sorteo guarda las
    /// suyas (spec 02 §1.2).
    /// </summary>
    public Result<Raffle, BaseError> Update(UpdateRaffleDto dto, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (State == RaffleState.Completed)
            return Result.Failure<Raffle, BaseError>(RaffleErrors.Completed());

        if (dto.RequiresCheckIn.HasValue && dto.RequiresCheckIn.Value != RequiresCheckIn && State != RaffleState.Draft)
            return Result.Failure<Raffle, BaseError>(RaffleErrors.NotDraft());

        return this with
        {
            VisualStyle = dto.VisualStyle,
            ShowPrizeOnScreen = dto.ShowPrizeOnScreen,
            AnimationSeconds = dto.AnimationSeconds,
            RequiresPresenceConfirmation = dto.RequiresPresenceConfirmation,
            RequiresCheckIn = dto.RequiresCheckIn ?? RequiresCheckIn,
            // Siempre: quien gana no vuelve a entrar al bombo (EligiblePool), se pida lo que se pida.
            OnePrizePerPerson = true,
            OnePrizePerCompany = dto.OnePrizePerCompany,
            LinkedEventId = dto.LinkedEventId,
            BoardTitle = dto.BoardTitle ?? BoardTitle,
            BoardSubtitle = dto.BoardSubtitle ?? BoardSubtitle,
            BoardIdentifier = dto.BoardIdentifier ?? BoardIdentifier,
            BoardBatchSize = dto.BoardBatchSize ?? BoardBatchSize,
            Authorities = dto.Authorities,
            PickupInstructions = dto.PickupInstructions,
            UpdatedAt = now,
            UpdatedById = dto.UpdatedById,
        };
    }

    /// <summary>
    /// Que el sorteo siga en borrador. Al arrancar se congela la lista de participantes, así que
    /// una decisión manual ya no tiene sobre qué actuar (spec 01 §9): <c>RAFFLE_NOT_DRAFT</c> en
    /// curso. Sobre un completado gana el error más específico, <c>RAFFLE_COMPLETED</c>, como en toda
    /// escritura salvo la entrega (spec 00, definición de terminado).
    /// </summary>
    public UnitResult<BaseError> EnsureDraft() => State switch
    {
        RaffleState.Draft => UnitResult.Success<BaseError>(),
        RaffleState.Completed => UnitResult.Failure(RaffleErrors.Completed()),
        _ => UnitResult.Failure(RaffleErrors.NotDraft()),
    };

    /// <summary>
    /// Que el sorteo esté en curso, para sortear o anular: <c>RAFFLE_NOT_IN_PROGRESS</c> en draft y
    /// <c>RAFFLE_COMPLETED</c> en un sorteo completado (spec 02 §7).
    /// </summary>
    public UnitResult<BaseError> EnsureInProgress() => State switch
    {
        RaffleState.InProgress => UnitResult.Success<BaseError>(),
        RaffleState.Completed => UnitResult.Failure(RaffleErrors.Completed()),
        _ => UnitResult.Failure(RaffleErrors.NotInProgress()),
    };

    /// <summary>
    /// Que el sorteo ya haya arrancado, para entregar un premio o revertir su entrega:
    /// <c>RAFFLE_NOT_IN_PROGRESS</c> en draft, y en curso o completado pasa. Los ganadores retiran en
    /// un mostrador días después, con el sorteo ya cerrado (spec 03 §3.4).
    /// </summary>
    public UnitResult<BaseError> EnsureStarted() => State == RaffleState.Draft
        ? UnitResult.Failure(RaffleErrors.NotInProgress())
        : UnitResult.Success<BaseError>();

    /// <summary>
    /// Pasa el sorteo a <c>in_progress</c> y deja escrito quién lo arrancó, cuándo y con qué lista de
    /// DO Travel Awards, que puede no haber. Solo desde draft: un segundo arranque es
    /// <c>RAFFLE_NOT_DRAFT</c>, nunca un segundo congelado, y uno sobre un completado
    /// <c>RAFFLE_COMPLETED</c>.
    /// </summary>
    public Result<Raffle, BaseError> Start(Guid? awardsBatchId, Guid startedById, DateTimeOffset now)
    {
        var isDraft = EnsureDraft();
        if (isDraft.IsFailure)
            return Result.Failure<Raffle, BaseError>(isDraft.Error);

        return this with
        {
            State = RaffleState.InProgress,
            StartedAt = now,
            StartedById = startedById,
            AwardsBatchId = awardsBatchId,
            UpdatedAt = now,
            UpdatedById = startedById,
        };
    }

    /// <summary>
    /// Pasa el sorteo a <c>completed</c> y deja escrito quién lo cerró y cuándo. Solo desde
    /// in_progress, y con lo que el acta y el correo al ganador necesitan: las seis autoridades y las
    /// instrucciones de retiro, en ese orden (spec 03 §4.4). Los premios sin sortear no lo impiden: el
    /// acta dice cuántos quedaron (spec 00 §2).
    /// </summary>
    public Result<Raffle, BaseError> Complete(Guid completedById, DateTimeOffset now)
    {
        var inProgress = EnsureInProgress();
        if (inProgress.IsFailure)
            return Result.Failure<Raffle, BaseError>(inProgress.Error);

        if (!Authorities.IsComplete)
            return Result.Failure<Raffle, BaseError>(RaffleErrors.AuthoritiesRequired());

        if (string.IsNullOrWhiteSpace(PickupInstructions))
            return Result.Failure<Raffle, BaseError>(RaffleErrors.PickupInstructionsRequired());

        return this with
        {
            State = RaffleState.Completed,
            CompletedAt = now,
            CompletedById = completedById,
            UpdatedAt = now,
            UpdatedById = completedById,
        };
    }

    /// <summary>
    /// Reemplaza las seis autoridades de un sorteo completado y deja escrito quién y cuándo, para la
    /// reemisión del acta (spec 03 §4.5). Solo sobre un completado: otro estado es
    /// <c>RAFFLE_NOT_COMPLETED</c>. El cierre —quién y cuándo se completó— no se toca: es lo que el
    /// acta atestigua, igual en todas sus versiones.
    /// </summary>
    public Result<Raffle, BaseError> Reissue(RaffleAuthorities authorities, Guid reissuedById, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(authorities);

        if (State != RaffleState.Completed)
            return Result.Failure<Raffle, BaseError>(RaffleErrors.NotCompleted());

        return this with
        {
            Authorities = authorities,
            UpdatedAt = now,
            UpdatedById = reissuedById,
        };
    }

    /// <summary>
    /// Reemplaza los firmantes del certificado de premio y deja escrito quién y cuándo. Vale en
    /// cualquier estado: el certificado se imprime sobre todo después del sorteo, y no es evidencia.
    /// </summary>
    public Raffle ChangeCertificateSigners(CertificateSigners signers, Guid changedById, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(signers);

        return this with
        {
            CertificateSigners = signers,
            UpdatedAt = now,
            UpdatedById = changedById,
        };
    }

    /// <summary>
    /// Reemplaza los campos visibles del participante mientras el sorteo esté en borrador (§10).
    /// Una vez iniciado, la configuración queda fija igual que el bombo.
    /// </summary>
    public Result<Raffle, BaseError> ChangeDisplayFields(
        RaffleDisplayFields displayFields,
        Guid changedById,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(displayFields);

        var draft = EnsureDraft();
        if (draft.IsFailure)
            return Result.Failure<Raffle, BaseError>(draft.Error);

        return this with
        {
            DisplayFields = displayFields,
            UpdatedAt = now,
            UpdatedById = changedById,
        };
    }

    /// <summary>
    /// Vuelve a draft y olvida el arranque: quién, cuándo y con qué lista de DO Travel Awards. Solo
    /// desde in_progress; que no haya sorteos lo comprueba quien llama, bajo el bloqueo. No toca
    /// <see cref="UpdatedAt"/>: eso dice que cambió el armado, y no cambió (ADR-0028).
    /// </summary>
    public Result<Raffle, BaseError> Reopen()
    {
        var inProgress = EnsureInProgress();
        if (inProgress.IsFailure)
            return Result.Failure<Raffle, BaseError>(inProgress.Error);

        return this with
        {
            State = RaffleState.Draft,
            StartedAt = null,
            StartedById = null,
            AwardsBatchId = null,
        };
    }
}
