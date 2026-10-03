using Application.Features.Shifts.Command.DeleteShiftCommand;
using Application.Features.Shifts.Command.SaveShiftCommand;
using Application.Features.Shifts.DTOs;
using Application.Features.Shifts.Query.GetShiftsQuery;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Response;
using System.Collections.Generic;

namespace Volt.Server.Controllers.Admin
{
    /// <summary>Rider shifts per city. Riders can only go online inside one of their shifts.</summary>
    [Route("api/admin/[controller]")]
    [ApiController]
    [Authorize]
    public class ShiftController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ShiftController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        [ProducesResponseType(typeof(List<ShiftDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetAll([FromQuery] int? cityId = null)
        {
            var result = await _mediator.Send(new GetShiftsQuery { CityId = cityId });
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HttpGet("{shiftId:int}")]
        [ProducesResponseType(typeof(ShiftDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetById(int shiftId)
        {
            var result = await _mediator.Send(new GetShiftsQuery { ShiftId = shiftId });
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            var shift = result.Value.FirstOrDefault();
            if (shift == null)
                return BadRequest(ProblemDetail.CreateProblemDetail($"Shift with ID {shiftId} not found"));
            return Ok(shift);
        }

        [HttpPost]
        [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] SaveShiftCommand command)
        {
            command = command with { ShiftId = 0 };
            var result = await _mediator.Send(command);
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HttpPut("{shiftId:int}")]
        [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Update(int shiftId, [FromBody] SaveShiftCommand command)
        {
            command = command with { ShiftId = shiftId };
            var result = await _mediator.Send(command);
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HttpDelete("{shiftId:int}")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Delete(int shiftId)
        {
            var result = await _mediator.Send(new DeleteShiftCommand { ShiftId = shiftId });
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }
    }
}
