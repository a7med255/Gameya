using Gameya.Application.Common;
using Gameya.Application.Features.Payments;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gameya.API.Controllers;

[ApiController, Route("api/payments"), Authorize(Policy = AppPolicies.CanRecordPayments)]
public sealed class PaymentsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? personId, [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate, [FromQuery] Gameya.Domain.Enums.PaymentMethod? method,
        [FromQuery] Guid? associationId, [FromQuery] int? periodNumber,
        [FromQuery] Gameya.Domain.Enums.PaymentEntryType? entryType, [FromQuery] string? payerSearch,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        Ok(await sender.Send(new GetPaymentsQuery(personId, fromDate, toDate, method, associationId,
            periodNumber, entryType, payerSearch, page, pageSize), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Record(RecordPaymentCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPost("preview")]
    public async Task<IActionResult> Preview([FromBody] PreviewPaymentRequest request, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new PreviewPaymentQuery(request.PersonId, request.Amount, request.AdvanceYear,
            request.AdvanceMonth, request.AdvanceAmount, request.AssociationIds, request.CreditAssociationId), cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetPaymentByIdQuery(id), cancellationToken));

    [HttpGet("page/{personId:guid}")]
    public async Task<IActionResult> PaymentPage(Guid personId, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetPaymentPageQuery(personId), cancellationToken));

    [HttpPost("{id:guid}/adjustments"), Authorize(Policy = AppPolicies.OwnerOrAdmin)]
    public async Task<IActionResult> Adjust(Guid id, AdjustPaymentRequest request, CancellationToken cancellationToken) =>
        Ok(new { id = await sender.Send(new AdjustPaymentCommand(id, request.Amount, request.Reason, request.Allocations, request.Credits), cancellationToken) });
}

public sealed record PreviewPaymentRequest(Guid PersonId, decimal Amount, int? AdvanceYear = null,
    int? AdvanceMonth = null, decimal? AdvanceAmount = null,
    IReadOnlyCollection<Guid>? AssociationIds = null, Guid? CreditAssociationId = null);

public sealed record AdjustPaymentRequest(decimal Amount, string Reason,
    IReadOnlyCollection<AdjustmentAllocationInput> Allocations, IReadOnlyCollection<AdjustmentCreditInput> Credits);
