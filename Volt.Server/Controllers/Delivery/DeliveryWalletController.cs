using Application.Common;
using Application.Features.DeliveryApp.DTOs;
using Application.Features.DeliveryApp.Query.GetRiderWalletQuery;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Response;

namespace Volt.Server.Controllers.Delivery
{
    /// <summary>Rider app: cash owed to the company and commission owed to the rider.</summary>
    [Route("api/delivery/[controller]")]
    [ApiController]
    [Authorize(Roles = "Delivery")]
    public class DeliveryWalletController : ControllerBase
    {
        private readonly IMediator _mediator;

        public DeliveryWalletController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("Summary")]
        [ProducesResponseType(typeof(RiderWalletDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Summary()
        {
            var result = await _mediator.Send(new GetRiderWalletQuery());
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HttpGet("Journals")]
        [ProducesResponseType(typeof(PagedResult<RiderJournalDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Journals([FromQuery] GetRiderJournalsQuery query)
        {
            var result = await _mediator.Send(query);
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }
    }
}
