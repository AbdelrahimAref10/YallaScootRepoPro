using Application.Common;
using Application.Features.Order.Command.AcceptMerchantOrderCommand;
using Application.Features.Order.Command.MarkMerchantHandoverToDeliveryCommand;
using Application.Features.Order.Command.RejectMerchantOrderCommand;
using Application.Features.Order.DTOs;
using Application.Features.Order.Query.GetMyMerchantJournalsQuery;
using Application.Features.Order.Query.GetMyMerchantLedgerQuery;
using Application.Features.Order.Query.GetMyMerchantOrderDetailQuery;
using Application.Features.Order.Query.GetMyMerchantOrdersQuery;
using Domain.Authorization;
using Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Authorization;
using Presentation.Response;

namespace Volt.Server.Controllers.Merchant
{
    [Route("api/merchant/[controller]")]
    [ApiController]
    [Authorize(Roles = AppRoleNames.Merchant)]
    public class MerchantOrderController : ControllerBase
    {
        private readonly IMediator _mediator;

        public MerchantOrderController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HasPermission(Permissions.Merchant.Orders.View)]
        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<MerchantPortalOrderListItemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetMyOrders(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] OrderState? state = null,
            [FromQuery] string? orderCode = null,
            [FromQuery] MerchantOrderResponseStatus? myResponseStatus = null,
            [FromQuery] bool? pendingOnly = null,
            [FromQuery] bool? awaitingHandoverOnly = null)
        {
            var result = await _mediator.Send(new GetMyMerchantOrdersQuery
            {
                PageNumber = pageNumber,
                PageSize = pageSize,
                State = state,
                OrderCode = orderCode,
                MyResponseStatus = myResponseStatus,
                PendingOnly = pendingOnly,
                AwaitingHandoverOnly = awaitingHandoverOnly
            });
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HasPermission(Permissions.Merchant.Orders.View)]
        [HttpGet("{orderId:int}")]
        [ProducesResponseType(typeof(MerchantPortalOrderDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetMyOrder(int orderId)
        {
            var result = await _mediator.Send(new GetMyMerchantOrderDetailQuery { OrderId = orderId });
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HasPermission(Permissions.Merchant.Payments.View)]
        [HttpGet("ledger")]
        [ProducesResponseType(typeof(PartyLedgerDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetMyLedger()
        {
            var result = await _mediator.Send(new GetMyMerchantLedgerQuery());
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HasPermission(Permissions.Merchant.Payments.View)]
        [HttpGet("journals")]
        [ProducesResponseType(typeof(OrderJournalListDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetMyJournals([FromQuery] int? orderId = null)
        {
            var result = await _mediator.Send(new GetMyMerchantJournalsQuery { OrderId = orderId });
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HasPermission(Permissions.Merchant.Orders.Edit)]
        [HttpPost("{orderId:int}/Accept")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> AcceptOrder(int orderId, [FromBody] AcceptMerchantOrderCommand? command)
        {
            command ??= new AcceptMerchantOrderCommand();
            command.OrderId = orderId;
            var result = await _mediator.Send(command);
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HasPermission(Permissions.Merchant.Orders.Edit)]
        [HttpPost("{orderId:int}/Reject")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RejectOrder(int orderId, [FromBody] RejectMerchantOrderCommand command)
        {
            command.OrderId = orderId;
            var result = await _mediator.Send(command);
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HasPermission(Permissions.Merchant.Orders.Edit)]
        [HttpPost("{orderId:int}/Handover")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> HandoverToDelivery(int orderId, [FromBody] MarkMerchantHandoverToDeliveryCommand command)
        {
            command.OrderId = orderId;
            var result = await _mediator.Send(command);
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }
    }
}
