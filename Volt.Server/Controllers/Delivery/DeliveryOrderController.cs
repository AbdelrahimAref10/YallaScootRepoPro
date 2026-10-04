using Application.Common;
using Application.Features.DeliveryApp.Command.SubmitRiderHandoverCommand;
using Application.Features.DeliveryApp.Command.UploadRiderHandoverImageCommand;
using Application.Features.DeliveryApp.DTOs;
using Application.Features.DeliveryApp.Query.GetRiderOrderDetailQuery;
using Application.Features.DeliveryApp.Query.GetRiderOrdersQuery;
using Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Response;

namespace Volt.Server.Controllers.Delivery
{
    /// <summary>Rider app: my orders, the four handover steps (4 photos each), and photo upload.</summary>
    [Route("api/delivery/[controller]")]
    [ApiController]
    [Authorize(Roles = "Delivery")]
    public class DeliveryOrderController : ControllerBase
    {
        private readonly IMediator _mediator;

        public DeliveryOrderController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<RiderOrderSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetMyOrders([FromQuery] GetRiderOrdersQuery query)
        {
            var result = await _mediator.Send(query);
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HttpGet("{orderId:int}")]
        [ProducesResponseType(typeof(RiderOrderDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetMyOrder(int orderId)
        {
            var result = await _mediator.Send(new GetRiderOrderDetailQuery { OrderId = orderId });
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        /// <summary>Multipart field "file". Returns the stored URL to send back with the step.</summary>
        [HttpPost("UploadImage")]
        [RequestSizeLimit(6 * 1024 * 1024)]
        [ProducesResponseType(typeof(UploadedImageDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(ProblemDetail.CreateProblemDetail("Image is required"));

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);

            var result = await _mediator.Send(new UploadRiderHandoverImageCommand { Content = stream.ToArray() });
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(new UploadedImageDto { Url = result.Value });
        }

        [HttpPost("{orderId:int}/vehicles/{vehicleId:int}/ReceivedFromOwner")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public Task<IActionResult> ReceivedFromOwner(int orderId, int vehicleId, [FromBody] SubmitRiderHandoverCommand command) =>
            Submit(orderId, vehicleId, HandoverStep.ReceivedFromOwner, command);

        [HttpPost("{orderId:int}/vehicles/{vehicleId:int}/DeliveredToCustomer")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public Task<IActionResult> DeliveredToCustomer(int orderId, int vehicleId, [FromBody] SubmitRiderHandoverCommand command) =>
            Submit(orderId, vehicleId, HandoverStep.DeliveredToCustomer, command);

        [HttpPost("{orderId:int}/vehicles/{vehicleId:int}/ReceivedFromCustomer")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public Task<IActionResult> ReceivedFromCustomer(int orderId, int vehicleId, [FromBody] SubmitRiderHandoverCommand command) =>
            Submit(orderId, vehicleId, HandoverStep.ReceivedFromCustomer, command);

        [HttpPost("{orderId:int}/vehicles/{vehicleId:int}/DeliveredToOwner")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public Task<IActionResult> DeliveredToOwner(int orderId, int vehicleId, [FromBody] SubmitRiderHandoverCommand command) =>
            Submit(orderId, vehicleId, HandoverStep.DeliveredToOwner, command);

        [HttpPost("{orderId:int}/vehicles/{vehicleId:int}/NotReceivedByCustomer")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> NotReceivedByCustomer(int orderId, int vehicleId, [FromBody] RiderNotReceivedByCustomerCommand command)
        {
            command = command with { OrderId = orderId, VehicleId = vehicleId };
            var result = await _mediator.Send(command);
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        private async Task<IActionResult> Submit(int orderId, int vehicleId, HandoverStep step, SubmitRiderHandoverCommand command)
        {
            command = command with { OrderId = orderId, VehicleId = vehicleId, Step = step };
            var result = await _mediator.Send(command);
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }
    }

    public class UploadedImageDto
    {
        public string Url { get; set; } = string.Empty;
    }
}
