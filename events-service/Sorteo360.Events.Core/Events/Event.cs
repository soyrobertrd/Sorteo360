using CSharpFunctionalExtensions;
using Sorteo360.Events.Core.DTOs;
using Sorteo360.Events.Core.Errors;
using Sorteo360.Events.Core.Shared;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Core.Events;

public sealed record Event
{
    public Guid Id { get; init; }
    // Las reglas de cada campo viven en su init y no en el constructor, porque un with copia el
    // registro y luego pasa solo por los init que nombra; el constructor nunca corre.
    public string Type { get; init => field = value ?? throw new ArgumentNullException(nameof(Type)); }
    public string Name { get; init => field = value ?? throw new ArgumentNullException(nameof(Name)); }
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public string Status { get; init => field = value ?? throw new ArgumentNullException(nameof(Status)); }

    /// <summary>
    /// El idioma en el que corre el evento. No es opcional: una convocatoria toma de acá la
    /// plantilla con la que sale, y sin idioma no hay plantilla que elegir, así que un nulo
    /// bloquearía el lanzamiento en vez de significar algo.
    /// </summary>
    public string Language { get; init => field = value ?? throw new ArgumentNullException(nameof(Language)); }

    /// <summary>
    /// Si el evento se publica o no. Obligatorio, y sin valor por omisión en la base: una fila
    /// nueva declara su visibilidad, porque adivinarla es exactamente lo que no se quiere.
    /// Quién lo decide es <c>events:write</c>, y puede cambiarlo en cualquier momento.
    /// </summary>
    public string Visibility { get; init => field = value ?? throw new ArgumentNullException(nameof(Visibility)); }

    /// <summary>Guarda el montaje del salón, uno de <see cref="Events.SeatingLayout.All"/>.</summary>
    /// <remarks>
    /// Es opcional en todo tipo de evento. Nulo quiere decir que nadie lo eligió todavía, y un
    /// evento sin montaje del salón no puede configurar asientos.
    /// </remarks>
    public string? SeatingLayout { get; init; }

    public string? ExternalUrl { get; init; }

    /// <summary>Texto libre que describe el evento. Se ve en la ficha y, más adelante, en su página pública.</summary>
    public string? Description { get; init; }

    public int? InvitedCount { get; init; }

    /// <summary>
    /// Lo que se presupuestó para el evento, tal como lo escribió la OPT. Convive con
    /// <see cref="TotalCost"/> —que se calcula al cerrar, con las cotizaciones aprobadas— y ninguna
    /// regla ata uno al otro: tener los dos es lo que permite ver cuánto se desviaron.
    /// </summary>
    public Money? EstimatedBudget
    {
        get;
        init => field = IsValidEstimatedBudget(value)
            ? value
            : throw new ArgumentException(
                "El presupuesto estimado debe ser mayor que cero.", nameof(EstimatedBudget));
    }

    public Money? TotalCost { get; init; }
    public string? CancellationReason { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public Guid CreatedById { get; init; }
    public Guid UpdatedById { get; init; }
    public DateTimeOffset? DeletedAt { get; init; }

    public Event(
        Guid id,
        string type,
        string name,
        DateOnly startDate,
        DateOnly endDate,
        string status,
        string language,
        string visibility,
        string? seatingLayout,
        string? externalUrl,
        string? description,
        int? invitedCount,
        Money? estimatedBudget,
        Money? totalCost,
        string? cancellationReason,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        Guid createdById,
        Guid updatedById,
        DateTimeOffset? deletedAt)
    {
        Id = id;
        Type = type;
        Name = name;
        StartDate = startDate;
        EndDate = endDate;
        Status = status;
        Language = language;
        Visibility = visibility;
        SeatingLayout = seatingLayout;
        ExternalUrl = externalUrl;
        Description = description;
        InvitedCount = invitedCount;
        EstimatedBudget = estimatedBudget;
        TotalCost = totalCost;
        CancellationReason = cancellationReason;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        CreatedById = createdById;
        UpdatedById = updatedById;
        DeletedAt = deletedAt;
    }

    /// <summary>Dice si un presupuesto estimado es admisible: ninguno, o uno con monto positivo.</summary>
    /// <remarks>
    /// La creación y la edición la consultan antes de construir el evento, además de lo que revisa
    /// el borde, porque quien llama al servicio sin pasar por HTTP también merece un rechazo con
    /// código y no una excepción convertida en 500. La moneda no se comprueba: <c>Currency</c> es
    /// un enum cerrado y una inválida ya no es representable.
    /// </remarks>
    public static bool IsValidEstimatedBudget(Money? budget) => budget is null or { Amount: > 0 };

    /// <summary>
    /// Reemplaza la representación editable entera (ADR-0009), mientras el evento no sea terminal.
    /// </summary>
    /// <remarks>
    /// Lo omitido llega en blanco y queda en blanco, y un evento cerrado o cancelado ya no se toca.
    /// <para>
    /// La visibilidad es la única excepción: omitirla la conserva, porque el formulario todavía no
    /// tiene ese control y borrarla despublicaría en silencio un evento anunciado (ver
    /// <see cref="UpdateEventDto"/>).
    /// </para>
    /// <para>
    /// Una edición no es dueña del tipo, que se fija al crear; ni del estado, el costo total o la
    /// razón de cancelación, que cambian solo por sus transiciones; ni de los datos de creación y
    /// borrado. Todos quedan como se cargaron. El presupuesto se revisa antes de copiar, para que un
    /// monto no positivo vuelva como error y no como la excepción que lanzaría su propiedad.
    /// </para>
    /// </remarks>
    public Result<Event, BaseError> Update(UpdateEventDto dto, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(dto);

        // Una edición de otro evento es un error de quien llama, no algo que el usuario pueda
        // provocar: el servicio carga el evento por el id del mismo dto.
        if (dto.Id != Id)
            throw new ArgumentException("La edición nombra otro evento.", nameof(dto));

        if (!EventStatus.Active.Contains(Status))
            return Result.Failure<Event, BaseError>(EventErrors.NotEditable());

        if (!IsValidEstimatedBudget(dto.EstimatedBudget))
            return Result.Failure<Event, BaseError>(EventErrors.BudgetAmountInvalid());

        return this with
        {
            Name = dto.Name,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Language = dto.Language,
            Visibility = dto.Visibility ?? Visibility,
            SeatingLayout = dto.SeatingLayout,
            ExternalUrl = dto.ExternalUrl,
            Description = dto.Description,
            InvitedCount = dto.InvitedCount,
            EstimatedBudget = dto.EstimatedBudget,
            UpdatedAt = now,
            UpdatedById = dto.UpdatedById,
        };
    }
}
