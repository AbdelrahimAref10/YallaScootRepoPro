using Application.Features.Auth.DTOs;
using Application.Features.Auth.Query.GetMyAccessQuery;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Response;

namespace Volt.Server.Controllers.General
{
    /// <summary>Sub-role and permissions of the signed-in admin / merchant user.</summary>
    [Route("api/general/[controller]")]
    [ApiController]
    [Authorize]
    public class AccessController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AccessController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("MyAccess")]
        [ProducesResponseType(typeof(AccessResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetMyAccess()
        {
            var result = await _mediator.Send(new GetMyAccessQuery());
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }
    }
}
