using Gameya.Application.Abstractions;
using Gameya.Application.Common;
using Gameya.Application.DTOs;
using FluentValidation;
using MediatR;

namespace Gameya.Application.Features.Reports;

public sealed record GetMonthlyReportQuery(int Year, int Month) : IRequest<GeneratedReportDto>;
public sealed class GetMonthlyReportValidator : AbstractValidator<GetMonthlyReportQuery>
{
    public GetMonthlyReportValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2200).WithErrorCode("report.invalid_period");
        RuleFor(x => x.Month).InclusiveBetween(1, 12).WithErrorCode("report.invalid_period");
    }
}
public sealed class GetMonthlyReportHandler(IReportGenerator reports, ICurrentUser current)
    : IRequestHandler<GetMonthlyReportQuery, GeneratedReportDto>
{
    public Task<GeneratedReportDto> Handle(GetMonthlyReportQuery request, CancellationToken cancellationToken) =>
        reports.GenerateMonthlyAsync(current.UserId ?? throw new ForbiddenException(), current.IsInRole(AppRoles.Admin),
            request.Year, request.Month, cancellationToken);
}
public sealed record SendMonthlyReportCommand(int Year, int Month) : IRequest<string>;
public sealed class SendMonthlyReportValidator : AbstractValidator<SendMonthlyReportCommand>
{
    public SendMonthlyReportValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2200).WithErrorCode("report.invalid_period");
        RuleFor(x => x.Month).InclusiveBetween(1, 12).WithErrorCode("report.invalid_period");
    }
}
public sealed class SendMonthlyReportHandler(IIdempotentReportJob reportJob, ICurrentUser current)
    : IRequestHandler<SendMonthlyReportCommand, string>
{
    public async Task<string> Handle(SendMonthlyReportCommand request, CancellationToken cancellationToken)
    {
        var executionKey = $"manual:{Guid.NewGuid():N}";
        await reportJob.ExecuteAsync(executionKey, current.UserId ?? throw new ForbiddenException(),
            current.IsInRole(AppRoles.Admin), request.Year, request.Month, cancellationToken);
        return executionKey;
    }
}

public sealed record GetAssociationReportQuery(Guid AssociationId) : IRequest<GeneratedReportDto>;
public sealed class GetAssociationReportHandler(IReportGenerator reports, ICurrentUser current)
    : IRequestHandler<GetAssociationReportQuery, GeneratedReportDto>
{
    public Task<GeneratedReportDto> Handle(GetAssociationReportQuery request, CancellationToken cancellationToken) =>
        reports.GenerateAssociationAsync(current.UserId ?? throw new ForbiddenException(),
            current.IsInRole(AppRoles.Admin), request.AssociationId, cancellationToken);
}

public sealed record SendAssociationReportCommand(Guid AssociationId) : IRequest;
public sealed class SendAssociationReportHandler(IReportDeliveryService delivery, ICurrentUser current)
    : IRequestHandler<SendAssociationReportCommand>
{
    public Task Handle(SendAssociationReportCommand request, CancellationToken cancellationToken) =>
        delivery.SendAssociationAsync(current.UserId ?? throw new ForbiddenException(),
            current.IsInRole(AppRoles.Admin), request.AssociationId, cancellationToken);
}
