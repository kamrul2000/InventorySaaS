using InventorySaaS.Application.Features.Picking.DTOs;

namespace InventorySaaS.Application.Services;

public interface IPickingService
{
    /// <summary>
    /// The current picking session for an order, or null when none is open. Always reports every
    /// order line with its requested, picked and remaining quantities.
    /// </summary>
    Task<PickSessionDto?> GetAsync(Guid salesOrderId, CancellationToken cancellationToken);

    /// <summary>
    /// Opens a picking run against a confirmed order. Re-calling this while a session is already
    /// open returns that session rather than starting a competing one.
    /// </summary>
    Task<PickSessionDto> StartAsync(
        Guid salesOrderId,
        StartPickRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records one pick. Rejects products that are not on the order and quantities beyond what
    /// the line still needs. <paramref name="idempotencyKey"/> makes a double-scan safe.
    /// </summary>
    Task<PickScanResultDto> ScanAsync(
        Guid salesOrderId,
        PickScanRequest request,
        CancellationToken cancellationToken,
        string? idempotencyKey = null);

    /// <summary>
    /// Closes the session once every line is fully picked, optionally handing the picked
    /// quantities to the existing delivery path.
    /// </summary>
    Task<PickSessionDto> CompleteAsync(
        Guid salesOrderId,
        CompletePickRequest request,
        CancellationToken cancellationToken);

    /// <summary>Abandons the run and clears the picked quantities it recorded.</summary>
    Task<PickSessionDto> CancelAsync(Guid salesOrderId, CancellationToken cancellationToken);
}
