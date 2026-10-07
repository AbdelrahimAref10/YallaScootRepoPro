using Application.Features.SubRoles.Command.CreateSubRoleCommand;
using Application.Features.SubRoles.Command.DeleteSubRoleCommand;
using Application.Features.SubRoles.Command.UpdateSubRoleCommand;
using Application.Features.SubRoles.DTOs;
using Application.Features.SubRoles.Query.GetPermissionsQuery;
using Application.Features.SubRoles.Query.GetSubRoleByIdQuery;
using Application.Features.SubRoles.Query.GetSubRolesQuery;
using Domain.Authorization;
using Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Authorization;
using Presentation.Response;

namespace Volt.Server.Controllers.Admin
{
    /// <summary>Sub-roles of the admin and merchant panels and their permissions.</summary>
    [Route("api/admin/[controller]")]
    [ApiController]
    [Authorize(Roles = AppRoleNames.SuperAdmin)]
    public class SubRoleController : ControllerBase
    {
        private readonly IMediator _mediator;

        public SubRoleController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>Also used as the sub-role dropdown of the system users form.</summary>
        [HasPermission(Permissions.Admin.Roles.View, Permissions.Admin.SystemUsers.View)]
        [HttpGet]
        [ProducesResponseType(typeof(List<SubRoleDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetAll([FromQuery] GetSubRolesQuery query)
        {
            var result = await _mediator.Send(query);
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HasPermission(Permissions.Admin.Roles.View)]
        [HttpGet("{subRoleId:int}")]
        [ProducesResponseType(typeof(SubRoleDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetById(int subRoleId)
        {
            var result = await _mediator.Send(new GetSubRoleByIdQuery(subRoleId));
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HasPermission(Permissions.Admin.Roles.View)]
        [HttpGet("Permissions")]
        [ProducesResponseType(typeof(List<PermissionModuleDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetPermissions([FromQuery] int scope)
        {
            var result = await _mediator.Send(new GetPermissionsQuery(scope));
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HasPermission(Permissions.Admin.Roles.Create)]
        [HttpPost]
        [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] CreateSubRoleCommand command)
        {
            var result = await _mediator.Send(command);
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HasPermission(Permissions.Admin.Roles.Edit)]
        [HttpPut("{subRoleId:int}")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Update(int subRoleId, [FromBody] UpdateSubRoleCommand command)
        {
            command.SubRoleId = subRoleId;
            var result = await _mediator.Send(command);
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HasPermission(Permissions.Admin.Roles.Delete)]
        [HttpDelete("{subRoleId:int}")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Delete(int subRoleId)
        {
            var result = await _mediator.Send(new DeleteSubRoleCommand(subRoleId));
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }
    }
}
