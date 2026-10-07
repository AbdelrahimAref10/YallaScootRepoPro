using Application.Features.MerchantNotification.Command.MarkAllMyMerchantNotificationsAsReadCommand;
using Application.Features.MerchantNotification.Command.MarkMerchantNotificationAsReadCommand;
using Application.Features.MerchantNotification.DTOs;
using Application.Features.MerchantNotification.Query.GetMyMerchantNotificationsQuery;
using Application.Features.MerchantNotification.Query.GetMyMerchantUnreadNotificationsCountQuery;
using Domain.Authorization;
using Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Authorization;
using Presentation.Response;
using System.Collections.Generic;

namespace Volt.Server.Controllers.Merchant
{
    [Route("api/merchant/[controller]")]
    [ApiController]
    [Authorize(Roles = AppRoleNames.Merchant)]
    public class MerchantNotificationController : ControllerBase
    {
        private readonly IMediator _mediator;

        public MerchantNotificationController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        [ProducesResponseType(typeof(List<MerchantNotificationDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetMyNotifications(
            [FromQuery] bool? isRead = null,
            [FromQuery] int? skip = null,
            [FromQuery] int? take = null)
        {
            var result = await _mediator.Send(new GetMyMerchantNotificationsQuery
            {
                IsRead = isRead,
                Skip = skip,
                Take = take
            });
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HttpGet("UnreadCount")]
        [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetMyUnreadNotificationCount()
        {
            var result = await _mediator.Send(new GetMyMerchantUnreadNotificationsCountQuery());
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HttpPost("{id}/MarkAsRead")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> MarkNotificationAsRead(int id)
        {
            var result = await _mediator.Send(new MarkMerchantNotificationAsReadCommand
            {
                MerchantNotificationId = id
            });
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok();
        }

        [HttpPost("MarkAllAsRead")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> MarkAllNotificationsAsRead()
        {
            var result = await _mediator.Send(new MarkAllMyMerchantNotificationsAsReadCommand());
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok();
        }
    }
}
