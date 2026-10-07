using Application.Common;
using Application.Features.Order.DTOs;
using Application.Features.Order.Query.GetAllOrderJournalsQuery;
using Application.Features.Order.Query.GetPartyLedgerQuery;
using Application.Features.Settlement.Command.CreateSettlementVoucherCommand;
using Application.Features.Settlement.DTOs;
using Application.Features.Settlement.Query;
using Domain.Authorization;
using Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Authorization;
using Presentation.Response;

namespace Volt.Server.Controllers.Admin
{
    /// <summary>
    /// Admin settlement APIs: journals, party ledger and settlement vouchers
    /// (collect from / pay a delivery, pay a merchant).
    /// </summary>
    [Route("api/admin/[controller]")]
    [ApiController]
    [Authorize(Roles = AppRoleNames.SuperAdmin)]
    public class AdminSettlementController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AdminSettlementController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>
        /// All journal movements for admin. Optional filters: orderCode, deliveryId, merchantId.
        /// Totals (credit/debit/balance) are over the filtered visible set. Balance = credit − debit.
        /// </summary>
        [HasPermission(Permissions.Admin.Journals.View)]
        [HttpGet("journals")]
        [ProducesResponseType(typeof(OrderJournalListDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetJournals([FromQuery] GetAllOrderJournalsQuery query)
        {
            var result = await _mediator.Send(query);
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        /// <summary>Ledger timeline + balance for a delivery or merchant (includes float rows).</summary>
        [HasPermission(Permissions.Admin.Settlements.View)]
        [HttpGet("ledger")]
        [ProducesResponseType(typeof(PartyLedgerDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetLedger([FromQuery] LedgerPartyType partyType, [FromQuery] int partyId)
        {
            var result = await _mediator.Send(new GetPartyLedgerQuery
            {
                PartyType = partyType,
                PartyId = partyId
            });
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        /// <summary>
        /// What the company and the merchant / delivery owe each other, and the open orders (oldest first)
        /// a voucher would settle.
        /// </summary>
        [HasPermission(Permissions.Admin.Settlements.View)]
        [HttpGet("Summary")]
        [ProducesResponseType(typeof(SettlementSummaryDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetSummary([FromQuery] LedgerPartyType partyType, [FromQuery] int partyId)
        {
            var result = await _mediator.Send(new GetSettlementSummaryQuery(partyType, partyId));
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        /// <summary>
        /// Collects from / pays a merchant or delivery (full or partial balance). Posts the party and company
        /// lines on the oldest open orders and a treasury record.
        /// </summary>
        [HasPermission(Permissions.Admin.Settlements.Create)]
        [HttpPost("Vouchers")]
        [ProducesResponseType(typeof(SettlementVoucherDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateVoucher([FromBody] CreateSettlementVoucherCommand command)
        {
            var result = await _mediator.Send(command);
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HasPermission(Permissions.Admin.Settlements.View)]
        [HttpGet("Vouchers")]
        [ProducesResponseType(typeof(PagedResult<SettlementVoucherDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetVouchers([FromQuery] GetSettlementVouchersQuery query)
        {
            var result = await _mediator.Send(query);
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }

        [HasPermission(Permissions.Admin.Settlements.View)]
        [HttpGet("Vouchers/{settlementVoucherId:int}")]
        [ProducesResponseType(typeof(SettlementVoucherDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetail), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetVoucher(int settlementVoucherId)
        {
            var result = await _mediator.Send(new GetSettlementVoucherByIdQuery(settlementVoucherId));
            if (result.IsFailure)
                return BadRequest(ProblemDetail.CreateProblemDetail(result.Error));
            return Ok(result.Value);
        }
    }
}
