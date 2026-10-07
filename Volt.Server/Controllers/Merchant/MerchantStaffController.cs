using Application.Features.MerchantStaff.Command.CreateMerchantStaffCommand;
using Application.Features.MerchantStaff.Command.DeleteMerchantStaffCommand;
using Application.Features.MerchantStaff.Command.SetMerchantStaffActiveCommand;
using Application.Features.MerchantStaff.Command.UpdateMerchantStaffCommand;
using Application.Features.MerchantStaff.DTOs;
using Application.Features.MerchantStaff.Query.GetMerchantSubRolesLookupQuery;
using Application.Features.MerchantStaff.Query.GetMyStaffByIdQuery;
using Application.Features.MerchantStaff.Query.GetMyStaffQuery;
using Domain.Authorization;
using Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Authorization;
using Presentation.Response;

namespace Volt.Server.Controllers.Merchant
{
    /// <summary>Owner / staff accounts of the signed-in merchant.</summary>
    [Route("api/merchant/[controller]")]
    [ApiController]
    [Authorize(Roles = AppRoleNames.Merchant)]
    public class MerchantStaffController : ControllerBase
    {
        private readonly IMediator _mediator;

        public MerchantStaffController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HasPermission(Permissions.Merchant.Staff.View)]
        [HttpGet]
        [ProducesResponseType(typeof(List<MerchantStaffDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetMyStaff()
        {
            var result = await _mediator.Send(new GetMyStaffQuery());
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HasPermission(Permissions.Merchant.Staff.View)]
        [HttpGet("{merchantUserId:int}")]
        [ProducesResponseType(typeof(MerchantStaffDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetStaffById(int merchantUserId)
        {
            var result = await _mediator.Send(new GetMyStaffByIdQuery(merchantUserId));
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HasPermission(Permissions.Merchant.Staff.Create, Permissions.Merchant.Staff.Edit)]
        [HttpGet("SubRoles")]
        [ProducesResponseType(typeof(List<SubRoleLookupDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetAvailableSubRoles()
        {
            var result = await _mediator.Send(new GetMerchantSubRolesLookupQuery());
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HasPermission(Permissions.Merchant.Staff.Create)]
        [HttpPost]
        [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateStaff([FromBody] CreateMerchantStaffCommand command)
        {
            var result = await _mediator.Send(command);
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HasPermission(Permissions.Merchant.Staff.Edit)]
        [HttpPut("{merchantUserId:int}")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateStaff(int merchantUserId, [FromBody] UpdateMerchantStaffCommand command)
        {
            command.MerchantUserId = merchantUserId;
            var result = await _mediator.Send(command);
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HasPermission(Permissions.Merchant.Staff.Edit)]
        [HttpPost("{merchantUserId:int}/activate")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ActivateStaff(int merchantUserId)
        {
            var result = await _mediator.Send(new SetMerchantStaffActiveCommand(merchantUserId, true));
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HasPermission(Permissions.Merchant.Staff.Edit)]
        [HttpPost("{merchantUserId:int}/deactivate")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DeactivateStaff(int merchantUserId)
        {
            var result = await _mediator.Send(new SetMerchantStaffActiveCommand(merchantUserId, false));
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HasPermission(Permissions.Merchant.Staff.Delete)]
        [HttpDelete("{merchantUserId:int}")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DeleteStaff(int merchantUserId)
        {
            var result = await _mediator.Send(new DeleteMerchantStaffCommand(merchantUserId));
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }
    }
}
