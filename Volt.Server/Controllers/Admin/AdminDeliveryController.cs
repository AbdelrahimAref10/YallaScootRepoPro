using Application.Features.Delivery.Command.AdminCreateDeliveryCommand;
using Application.Features.Delivery.Command.AdminSoftDeleteDeliveryCommand;
using Application.Features.Delivery.Command.AdminUpdateDeliveryCommand;
using Application.Features.Delivery.Command.SetDeliveryCashDebtLimitCommand;
using Application.Features.Delivery.DTOs;
using Application.Features.Delivery.Query.GetActiveDeliveriesLookupQuery;
using Application.Features.Delivery.Query.GetAllDeliveriesQuery;
using Application.Features.Delivery.Query.GetDeliveriesForAssignQuery;
using Application.Features.Delivery.Query.GetDeliveryByIdQuery;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Response;
using System.Collections.Generic;

namespace Volt.Server.Controllers.Admin
{
    [Route("api/admin/[controller]")]
    [ApiController]
    [Authorize]
    public class AdminDeliveryController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AdminDeliveryController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        [ProducesResponseType(typeof(List<DeliveryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetAll([FromQuery] GetAllDeliveriesQuery query)
        {
            var result = await _mediator.Send(query);
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HttpGet("active")]
        [ProducesResponseType(typeof(List<DeliveryLookupDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetActive([FromQuery] int? cityId = null)
        {
            var result = await _mediator.Send(new GetActiveDeliveriesLookupQuery { CityId = cityId });
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        /// <summary>
        /// Riders for the assign / reassign dialog: in shift and online first, then in shift but offline,
        /// then off shift; with their open trips count.
        /// </summary>
        [HttpGet("ForAssign")]
        [ProducesResponseType(typeof(List<DeliveryAssignCandidateDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetForAssign([FromQuery] int? orderId = null, [FromQuery] int? cityId = null)
        {
            var result = await _mediator.Send(new GetDeliveriesForAssignQuery { OrderId = orderId, CityId = cityId });
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HttpGet("{deliveryId:int}")]
        [ProducesResponseType(typeof(DeliveryDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetById(int deliveryId)
        {
            var result = await _mediator.Send(new GetDeliveryByIdQuery { DeliveryId = deliveryId });
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HttpPost]
        [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] AdminCreateDeliveryCommand command)
        {
            var result = await _mediator.Send(command);
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HttpPut("{deliveryId:int}")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Update(int deliveryId, [FromBody] AdminUpdateDeliveryCommand command)
        {
            command.DeliveryId = deliveryId;
            var result = await _mediator.Send(command);
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        /// <summary>Max collected cash the rider may hold before he must remit. Null removes the limit.</summary>
        [HttpPut("{deliveryId:int}/CashDebtLimit")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SetCashDebtLimit(int deliveryId, [FromBody] SetDeliveryCashDebtLimitCommand command)
        {
            command.DeliveryId = deliveryId;
            var result = await _mediator.Send(command);
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        /// <summary>Soft delete — sets IsDeleted=true; row stays in DB.</summary>
        [HttpDelete("{deliveryId:int}")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Delete(int deliveryId)
        {
            var result = await _mediator.Send(new AdminSoftDeleteDeliveryCommand { DeliveryId = deliveryId });
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }
    }
}
