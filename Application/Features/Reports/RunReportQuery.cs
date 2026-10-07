using CSharpFunctionalExtensions;
using MediatR;

namespace Application.Features.Reports
{
    /// <summary>Runs report <paramref name="Key"/> of <see cref="ReportContext.Scope"/> with the given filters.</summary>
    public record RunReportQuery(string Key, ReportFilter Filter, ReportContext Context) : IRequest<Result<ReportResultDto>>;

    public class RunReportQueryHandler : IRequestHandler<RunReportQuery, Result<ReportResultDto>>
    {
        private readonly IEnumerable<IReport> _reports;

        public RunReportQueryHandler(IEnumerable<IReport> reports)
        {
            _reports = reports;
        }

        public async Task<Result<ReportResultDto>> Handle(RunReportQuery request, CancellationToken cancellationToken)
        {
            var report = _reports.FirstOrDefault(r => r.Scope == request.Context.Scope
                                                      && string.Equals(r.Key, request.Key, StringComparison.OrdinalIgnoreCase));
            if (report == null)
                return Result.Failure<ReportResultDto>($"Unknown report '{request.Key}'");
            if (request.Context.Scope == ReportScope.Merchant && request.Context.MerchantId == null)
                return Result.Failure<ReportResultDto>("Merchant profile not found for current user");
            if (request.Filter.FromDate > request.Filter.ToDate)
                return Result.Failure<ReportResultDto>("From date must be before To date");

            return Result.Success(await report.RunAsync(request.Filter, request.Context, cancellationToken));
        }
    }
}
