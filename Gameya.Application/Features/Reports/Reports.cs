using Gameya.Application.Abstractions;
using Gameya.Application.Common;
using Gameya.Application.DTOs;
using MediatR;

namespace Gameya.Application.Features.Reports;

public sealed record GetMonthlyReportQuery(int Year, int Month) : IRequest<GeneratedReportDto>;
public sealed class GetMonthlyReportHandler(IReportGenerator reports, ICurrentUser current)
    : IRequestHandler<GetMonthlyReportQuery, GeneratedReportDto>
{
    public Task<GeneratedReportDto> Handle(GetMonthlyReportQuery request, CancellationToken cancellationToken) =>
        reports.GenerateMonthlyAsync(current.UserId ?? throw new ForbiddenException(), request.Year, request.Month, cancellationToken);
}
public sealed record SendMonthlyReportCommand(int Year, int Month) : IRequest<string>;
public sealed class SendMonthlyReportHandler(IBackgroundJobScheduler jobs, ICurrentUser current)
    : IRequestHandler<SendMonthlyReportCommand, string>
{
    public Task<string> Handle(SendMonthlyReportCommand request, CancellationToken cancellationToken) =>
        Task.FromResult(jobs.EnqueueMonthlyReport(current.UserId ?? throw new ForbiddenException(), request.Year, request.Month));
}
