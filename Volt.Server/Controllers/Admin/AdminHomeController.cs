using Application.Features.AdminHome;
using Domain.Authorization;
using Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Authorization;
using Presentation.Response;

namespace Volt.Server.Controllers.Admin
{
    [Route("api/admin/[controller]")]
    [ApiController]
    [Authorize(Roles = AppRoleNames.SuperAdmin)]
    public class AdminHomeController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AdminHomeController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>The whole admin home page in one call: profit, balances, orders, fleet and leaders.</summary>
        [HasPermission(Permissions.Admin.Dashboard.View)]
        [HttpGet("Dashboard")]
        [ProducesResponseType(typeof(DashboardDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetDashboard([FromQuery] GetDashboardQuery query)
        {
            var result = await _mediator.Send(query);
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }
    }
}
