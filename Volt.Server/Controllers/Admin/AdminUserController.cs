using Application.Common;
using Application.Features.User.Command.ActivateUserCommand;
using Application.Features.User.Command.CreateUserCommand;
using Application.Features.User.Command.DeactivateUserCommand;
using Application.Features.User.Command.DeleteUserCommand;
using Application.Features.User.Command.UpdateUserCommand;
using Application.Features.User.Query.GetAllUsersQuery;
using Application.Features.User.Query.GetCurrentUserQuery;
using Application.Features.User.Query.GetUserByIdQuery;
using Domain.Authorization;
using Domain.Common;
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
    public class AdminUserController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IAuthorizationService _authorizationService;
        private readonly IUserSession _userSession;

        public AdminUserController(IMediator mediator, IAuthorizationService authorizationService, IUserSession userSession)
        {
            _mediator = mediator;
            _authorizationService = authorizationService;
            _userSession = userSession;
        }

        [HasPermission(Permissions.Admin.SystemUsers.View)]
        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<Application.Features.User.DTOs.UserDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetAll([FromQuery] GetAllUsersQuery query)
        {
            var result = await _mediator.Send(query);
            if (result.IsFailure)
            {
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            }
            return Ok(result.Value);
        }

        [HttpGet("current")]
        [ProducesResponseType(typeof(Application.Features.User.DTOs.UserDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetCurrent()
        {
            var query = new GetCurrentUserQuery();
            var result = await _mediator.Send(query);
            if (result.IsFailure)
            {
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            }
            return Ok(result.Value);
        }

        [HasPermission(Permissions.Admin.SystemUsers.View)]
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(Application.Features.User.DTOs.UserDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int id)
        {
            var query = new GetUserByIdQuery(id);
            var result = await _mediator.Send(query);
            if (result.IsFailure)
            {
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            }
            return Ok(result.Value);
        }

        [HasPermission(Permissions.Admin.SystemUsers.Create)]
        [HttpPost]
        [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] CreateUserCommand command)
        {
            var result = await _mediator.Send(command);
            if (result.IsFailure)
            {
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            }
            return Ok(result.Value);
        }

        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateUserCommand command)
        {
            // Anyone may edit their own profile; editing other users needs SystemUsers.Edit.
            if (id != _userSession.UserId)
            {
                var authorization = await _authorizationService.AuthorizeAsync(User, new HasPermissionAttribute(Permissions.Admin.SystemUsers.Edit).Policy!);
                if (!authorization.Succeeded)
                    return Forbid();
            }

            command.UserId = id;
            var result = await _mediator.Send(command);
            if (result.IsFailure)
            {
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            }
            return Ok();
        }

        [HasPermission(Permissions.Admin.SystemUsers.Delete)]
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Delete(int id)
        {
            var command = new DeleteUserCommand(id);
            var result = await _mediator.Send(command);
            if (result.IsFailure)
            {
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            }
            return Ok();
        }

        [HasPermission(Permissions.Admin.SystemUsers.Edit)]
        [HttpPost("{id}/activate")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Activate(int id)
        {
            var command = new ActivateUserCommand { UserId = id };
            var result = await _mediator.Send(command);
            if (result.IsFailure)
            {
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            }
            return Ok();
        }

        [HasPermission(Permissions.Admin.SystemUsers.Edit)]
        [HttpPost("{id}/deactivate")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Deactivate(int id)
        {
            var command = new DeactivateUserCommand { UserId = id };
            var result = await _mediator.Send(command);
            if (result.IsFailure)
            {
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            }
            return Ok();
        }
    }
}


