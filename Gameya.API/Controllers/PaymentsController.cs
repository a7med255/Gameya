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
    public async Task<IActionResult> List([FromQuery] Guid? personId, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        Ok(await sender.Send(new GetPaymentsQuery(personId, page, pageSize), cancellationToken));
    [HttpPost]
    public async Task<IActionResult> Record(RecordPaymentCommand command, CancellationToken cancellationToken)
    { var result = await sender.Send(command, cancellationToken); return CreatedAtAction(nameof(Get), new { id = result.Id }, result); }
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) => Ok(await sender.Send(new GetPaymentByIdQuery(id), cancellationToken));
    [HttpGet("page/{personId:guid}")]
    public async Task<IActionResult> PaymentPage(Guid personId, CancellationToken cancellationToken) => Ok(await sender.Send(new GetPaymentPageQuery(personId), cancellationToken));
    [HttpPost("{id:guid}/adjustments"), Authorize(Policy = AppPolicies.OwnerOrAdmin)]
    public async Task<IActionResult> Adjust(Guid id, AdjustPaymentRequest request, CancellationToken cancellationToken) =>
        Ok(new { id = await sender.Send(new AdjustPaymentCommand(id, request.Amount, request.Reason, request.Allocations, request.Credits), cancellationToken) });
}
public sealed record AdjustPaymentRequest(decimal Amount, string Reason,
    IReadOnlyCollection<AdjustmentAllocationInput> Allocations, IReadOnlyCollection<AdjustmentCreditInput> Credits);
