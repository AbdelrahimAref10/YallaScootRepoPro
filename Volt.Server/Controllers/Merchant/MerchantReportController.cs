using Application.Features.Reports;
using Domain.Authorization;
using Domain.Common;
using Domain.Enums;
using Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Presentation.Authorization;
using Presentation.Response;

namespace Volt.Server.Controllers.Merchant
{
    /// <summary>Merchant reports (orders, account statement, payments received, vehicles), limited to the signed-in merchant.</summary>
    [Route("api/merchant/[controller]")]
    [ApiController]
    [Authorize(Roles = AppRoleNames.Merchant)]
    public class MerchantReportController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly DatabaseContext _context;
        private readonly IUserSession _userSession;

        public MerchantReportController(IMediator mediator, DatabaseContext context, IUserSession userSession)
        {
            _mediator = mediator;
            _context = context;
            _userSession = userSession;
        }

        [HasPermission(Permissions.Merchant.Reports.View)]
        [HttpGet("{key}")]
        [ProducesResponseType(typeof(ReportResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Run(string key, [FromQuery] ReportFilter filter)
        {
            var result = await _mediator.Send(new RunReportQuery(key, filter, await ContextAsync()));
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HasPermission(Permissions.Merchant.Reports.View)]
        [HttpGet("{key}/Export")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Export(string key, [FromQuery] ReportFilter filter, [FromQuery] ReportExportFormat format = ReportExportFormat.Excel)
        {
            var result = await _mediator.Send(new RunReportQuery(key, filter, await ContextAsync()));
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            var file = ReportExporter.Export(result.Value, format, filter);
            return File(file.Content, file.ContentType, file.FileName);
        }

        private async Task<ReportContext> ContextAsync()
        {
            var merchantId = await _context.MerchantUsers.AsNoTracking()
                .Where(mu => mu.UserId == _userSession.UserId && mu.IsActive && !mu.IsDeleted)
                .Select(mu => (int?)mu.MerchantId)
                .FirstOrDefaultAsync();
            return new ReportContext(ReportScope.Merchant, merchantId);
        }
    }
}
