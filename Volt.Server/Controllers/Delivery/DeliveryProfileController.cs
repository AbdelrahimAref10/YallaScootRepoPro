using Application.Features.DeliveryApp.Command.SaveRiderDeviceCommand;
using Application.Features.DeliveryApp.DTOs;
using Application.Features.DeliveryApp.Query.GetRiderProfileQuery;
using Application.Features.DeliveryApp.Query.GetRiderShiftStatusQuery;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Response;

namespace Volt.Server.Controllers.Delivery
{
    /// <summary>Rider app: profile, FCM devices, shift and online/offline.</summary>
    [Route("api/delivery/[controller]")]
    [ApiController]
    [Authorize(Roles = "Delivery")]
    public class DeliveryProfileController : ControllerBase
    {
        private readonly IMediator _mediator;

        public DeliveryProfileController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("me")]
        [ProducesResponseType(typeof(RiderProfileDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Me()
        {
            var result = await _mediator.Send(new GetRiderProfileQuery());
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HttpPost("Devices")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SaveDevice([FromBody] SaveRiderDeviceCommand command)
        {
            var result = await _mediator.Send(command);
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HttpDelete("Devices")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RemoveDevice([FromQuery] string token)
        {
            var result = await _mediator.Send(new RemoveRiderDeviceCommand { Token = token });
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HttpGet("Shift")]
        [ProducesResponseType(typeof(RiderShiftStatusDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ShiftStatus()
        {
            var result = await _mediator.Send(new GetRiderShiftStatusQuery());
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HttpPost("Online")]
        [ProducesResponseType(typeof(RiderShiftStatusDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SetOnline([FromBody] SetRiderOnlineCommand command)
        {
            var result = await _mediator.Send(command);
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }
    }
}
