using Application.Features.AdminHome.DTOs;
using Application.Features.AdminHome.Query.GetHomeCancellationsQuery;
using Application.Features.AdminHome.Query.GetHomeCityPerformanceQuery;
using Application.Features.AdminHome.Query.GetHomeCustomerGrowthQuery;
using Application.Features.AdminHome.Query.GetHomeOrderPipelineQuery;
using Application.Features.AdminHome.Query.GetHomePaymentsMixQuery;
using Application.Features.AdminHome.Query.GetHomeRecentActivityQuery;
using Application.Features.AdminHome.Query.GetHomeRevenueTrendQuery;
using Application.Features.AdminHome.Query.GetHomeSummaryQuery;
using Application.Features.AdminHome.Query.GetHomeTopPerformersQuery;
using Application.Features.AdminHome.Query.GetHomeTreasurySnapshotQuery;
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

        [HasPermission(Permissions.Admin.Dashboard.View)]
        [HttpGet("Summary")]
        [ProducesResponseType(typeof(HomeSummaryDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetSummary([FromQuery] GetHomeSummaryQuery query)
        {
            return await Send(query);
        }

        [HasPermission(Permissions.Admin.Dashboard.View)]
        [HttpGet("RevenueTrend")]
        [ProducesResponseType(typeof(HomeRevenueTrendDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetRevenueTrend([FromQuery] GetHomeRevenueTrendQuery query)
        {
            return await Send(query);
        }

        [HasPermission(Permissions.Admin.Dashboard.View)]
        [HttpGet("OrderPipeline")]
        [ProducesResponseType(typeof(HomeOrderPipelineDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetOrderPipeline([FromQuery] GetHomeOrderPipelineQuery query)
        {
            return await Send(query);
        }

        [HasPermission(Permissions.Admin.Dashboard.View)]
        [HttpGet("CustomerGrowth")]
        [ProducesResponseType(typeof(HomeCustomerGrowthDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetCustomerGrowth([FromQuery] GetHomeCustomerGrowthQuery query)
        {
            return await Send(query);
        }

        [HasPermission(Permissions.Admin.Dashboard.View)]
        [HttpGet("PaymentsMix")]
        [ProducesResponseType(typeof(HomePaymentsMixDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetPaymentsMix([FromQuery] GetHomePaymentsMixQuery query)
        {
            return await Send(query);
        }

        [HasPermission(Permissions.Admin.Dashboard.View)]
        [HttpGet("TreasurySnapshot")]
        [ProducesResponseType(typeof(HomeTreasurySnapshotDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetTreasurySnapshot([FromQuery] GetHomeTreasurySnapshotQuery query)
        {
            return await Send(query);
        }

        [HasPermission(Permissions.Admin.Dashboard.View)]
        [HttpGet("Cancellations")]
        [ProducesResponseType(typeof(HomeCancellationsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetCancellations([FromQuery] GetHomeCancellationsQuery query)
        {
            return await Send(query);
        }

        [HasPermission(Permissions.Admin.Dashboard.View)]
        [HttpGet("TopPerformers")]
        [ProducesResponseType(typeof(HomeTopPerformersDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetTopPerformers([FromQuery] GetHomeTopPerformersQuery query)
        {
            return await Send(query);
        }

        [HasPermission(Permissions.Admin.Dashboard.View)]
        [HttpGet("CityPerformance")]
        [ProducesResponseType(typeof(HomeCityPerformanceDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetCityPerformance([FromQuery] GetHomeCityPerformanceQuery query)
        {
            return await Send(query);
        }

        [HasPermission(Permissions.Admin.Dashboard.View)]
        [HttpGet("RecentActivity")]
        [ProducesResponseType(typeof(HomeRecentActivityDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetRecentActivity([FromQuery] GetHomeRecentActivityQuery query)
        {
            return await Send(query);
        }

        private async Task<IActionResult> Send<T>(IRequest<CSharpFunctionalExtensions.Result<T>> query)
        {
            var result = await _mediator.Send(query);
            if (result.IsFailure)
            {
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            }

            return Ok(result.Value);
        }
    }
}
