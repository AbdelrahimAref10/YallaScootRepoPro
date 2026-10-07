using Application.Features.Support.Command.CreateSupportCommand;
using Application.Features.Support.Command.UpdateSupportCommand;
using Application.Features.Support.DTOs;
using Application.Features.Support.Query.GetSupportQuery;
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
    public class AdminSupportController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AdminSupportController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HasPermission(Permissions.Admin.Support.View)]
        [HttpGet]
        [ProducesResponseType(typeof(SupportDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Get()
        {
            var result = await _mediator.Send(new GetSupportQuery());
            if (result.IsFailure)
            {
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            }
            return Ok(result.Value);
        }

        [HasPermission(Permissions.Admin.Support.Edit)]
        [HttpPost]
        [ProducesResponseType(typeof(SupportDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] CreateSupportCommand command)
        {
            var result = await _mediator.Send(command);
            if (result.IsFailure)
            {
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            }
            return Ok(result.Value);
        }

        [HasPermission(Permissions.Admin.Support.Edit)]
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(SupportDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateSupportCommand command)
        {
            command.SupportId = id;
            var result = await _mediator.Send(command);
            if (result.IsFailure)
            {
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            }
            return Ok(result.Value);
        }
    }
}
