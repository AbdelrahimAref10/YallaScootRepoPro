using Application.Features.Merchant.DTOs;
using Application.Features.Merchant.Query.GetActiveMerchantsLookupQuery;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Response;
using System.Collections.Generic;

namespace Volt.Server.Controllers.General
{
    /// <summary>Shared merchant lookup (admin UI). Portal APIs live under Controllers/Merchant.</summary>
    [Route("api/general/[controller]")]
    [ApiController]
    [Authorize]
    public class MerchantController : ControllerBase
    {
        private readonly IMediator _mediator;

        public MerchantController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("active")]
        [ProducesResponseType(typeof(List<MerchantLookupDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetActive()
        {
            var result = await _mediator.Send(new GetActiveMerchantsLookupQuery());
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }
    }
}
