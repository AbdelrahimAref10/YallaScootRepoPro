using Application.Common;
using Application.Features.Merchant.Command.MerchantCreateVehicleCommand;
using Application.Features.Merchant.Command.MerchantDeleteVehicleCommand;
using Application.Features.Merchant.Command.MerchantUpdateVehicleCommand;
using Application.Features.Merchant.Query.GetMyMerchantVehicleByIdQuery;
using Application.Features.Merchant.Query.GetMyMerchantVehiclesQuery;
using Application.Features.Vehicle.DTOs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Response;

namespace Volt.Server.Controllers.Merchant
{
    [Route("api/merchant/[controller]")]
    [ApiController]
    [Authorize]
    public class MerchantVehicleController : ControllerBase
    {
        private readonly IMediator _mediator;

        public MerchantVehicleController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<VehicleDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetMyVehicles(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? searchTerm = null,
            [FromQuery] int? status = null,
            [FromQuery] int? categoryId = null,
            [FromQuery] int? subCategoryId = null)
        {
            var result = await _mediator.Send(new GetMyMerchantVehiclesQuery
            {
                PageNumber = pageNumber,
                PageSize = pageSize,
                SearchTerm = searchTerm,
                Status = status,
                CategoryId = categoryId,
                SubCategoryId = subCategoryId
            });
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HttpGet("{vehicleId}")]
        [ProducesResponseType(typeof(VehicleDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetMyVehicle(int vehicleId)
        {
            var result = await _mediator.Send(new GetMyMerchantVehicleByIdQuery { VehicleId = vehicleId });
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HttpPost]
        [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateVehicle([FromBody] MerchantCreateVehicleCommand command)
        {
            var result = await _mediator.Send(command);
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HttpPut]
        [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateVehicle([FromBody] MerchantUpdateVehicleCommand command)
        {
            var result = await _mediator.Send(command);
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HttpDelete("{vehicleId}")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DeleteVehicle(int vehicleId)
        {
            var result = await _mediator.Send(new MerchantDeleteVehicleCommand { VehicleId = vehicleId });
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }
    }
}
