using Application.Features.Delivery.Common;
using Application.Features.Settlement.DTOs;
using CSharpFunctionalExtensions;
using Domain.Common;
using Domain.Enums;
using Domain.Models;
using Infrastructure;
using Infrastructure.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Settlement.Command.CreateSettlementVoucherCommand
{
    /// <summary>
    /// Collects cash from / pays cash to a merchant or delivery against their whole open balance.
    /// The amount may be partial; it is applied to the oldest open orders first.
    /// </summary>
    public record CreateSettlementVoucherCommand : IRequest<Result<SettlementVoucherDetailDto>>
    {
        public LedgerPartyType PartyType { get; set; }
        public int PartyId { get; set; }
        /// <summary>Cash handed over; at most the summary's MaxAmount.</summary>
        public decimal Amount { get; set; }
        public string? Note { get; set; }
        /// <summary>Generated once per form; resubmitting returns the same voucher.</summary>
        public Guid RequestId { get; set; }
    }

    public class CreateSettlementVoucherCommandHandler : IRequestHandler<CreateSettlementVoucherCommand, Result<SettlementVoucherDetailDto>>
    {
        private readonly DatabaseContext _context;
        private readonly IUserSession _userSession;
        private readonly IRiderNotifier _riderNotifier;
        private readonly IMerchantNotificationHubService _merchantNotifications;

        public CreateSettlementVoucherCommandHandler(
            DatabaseContext context,
            IUserSession userSession,
            IRiderNotifier riderNotifier,
            IMerchantNotificationHubService merchantNotifications)
        {
            _context = context;
            _userSession = userSession;
            _riderNotifier = riderNotifier;
            _merchantNotifications = merchantNotifications;
        }

        public async Task<Result<SettlementVoucherDetailDto>> Handle(CreateSettlementVoucherCommand request, CancellationToken cancellationToken)
        {
            if (request.PartyType is not (LedgerPartyType.Merchant or LedgerPartyType.Delivery))
                return Result.Failure<SettlementVoucherDetailDto>("Settlements are only for merchants and deliveries");
            if (request.RequestId == Guid.Empty)
                return Result.Failure<SettlementVoucherDetailDto>("RequestId is required");
            if (request.Amount < 0)
                return Result.Failure<SettlementVoucherDetailDto>("Amount cannot be negative");
            var amount = Math.Round(request.Amount, 2);

            var existing = await FindByRequestAsync(request.RequestId, cancellationToken);
            if (existing != null)
                return Result.Success(existing);

            var actor = _userSession.UserName ?? "Admin";
            SettlementVoucher voucher;

            await using (var transaction = await _context.Database.BeginTransactionAsync(cancellationToken))
            {
                // One settlement per party at a time, so balances can't be spent twice.
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"EXEC sp_getapplock @Resource = {$"settle:{(int)request.PartyType}:{request.PartyId}"}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000",
                    cancellationToken);

                existing = await FindByRequestAsync(request.RequestId, cancellationToken);
                if (existing != null)
                    return Result.Success(existing);

                var summaryResult = await SettlementBalances.GetAsync(_context, request.PartyType, request.PartyId, cancellationToken);
                if (summaryResult.IsFailure)
                    return Result.Failure<SettlementVoucherDetailDto>(summaryResult.Error);
                var summary = summaryResult.Value;

                if (summary.OpenItems.Count == 0)
                    return Result.Failure<SettlementVoucherDetailDto>("Nothing is open for this party");
                if (amount > summary.MaxAmount)
                    return Result.Failure<SettlementVoucherDetailDto>($"Amount cannot be more than {summary.MaxAmount:0.##}");
                if (amount == 0 && (summary.CashOwedToCompany == 0 || summary.OwedByCompany == 0))
                    return Result.Failure<SettlementVoucherDetailDto>("Amount must be greater than zero");

                voucher = SettlementVoucher.Create(request.PartyType, request.PartyId, summary.Direction!.Value, amount, request.RequestId, request.Note, actor);
                _context.SettlementVouchers.Add(voucher);
                await _context.SaveChangesAsync(cancellationToken);
                voucher.AssignNumber();

                // Delivery: the smaller side is offset in full, the larger side is settled up to offset + cash.
                // Merchant: only earnings, settled up to the cash paid.
                var collecting = summary.Direction == SettlementDirection.CollectFromParty;
                var smallSide = request.PartyType == LedgerPartyType.Delivery
                    ? (collecting ? summary.OwedByCompany : summary.CashOwedToCompany)
                    : 0m;
                var largeSideAmount = smallSide + amount;

                foreach (var item in summary.OpenItems)
                {
                    var isCash = item.Kind is SettlementAllocationKind.DeliveryCash or SettlementAllocationKind.DeliveryLegacyFloat;
                    var onLargeSide = request.PartyType == LedgerPartyType.Merchant || isCash == collecting;

                    decimal take;
                    if (onLargeSide)
                    {
                        take = Math.Min(item.Open, largeSideAmount);
                        largeSideAmount -= take;
                    }
                    else
                    {
                        take = item.Open;
                    }

                    if (take <= 0)
                        continue;

                    voucher.AddAllocation(item.OrderId, item.Kind, take);
                    PostLines(voucher, item, take, actor);
                }

                if (amount > 0)
                {
                    var verb = collecting ? "Received from" : "Paid to";
                    var party = request.PartyType == LedgerPartyType.Delivery ? "delivery" : "merchant";
                    var description = $"{voucher.VoucherNo}: {verb} {party} {summary.PartyName}";
                    _context.CompanyTreasuries.Add(CompanyTreasury.Create(
                        debitAmount: collecting ? amount : 0,
                        creditAmount: collecting ? 0 : amount,
                        descriptionAr: description,
                        descriptionEng: description,
                        createdBy: actor));
                }

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }

            await NotifyPartyAsync(voucher, cancellationToken);

            return Result.Success((await FindByRequestAsync(request.RequestId, cancellationToken))!);
        }

        /// <summary>Party line (same kinds the rider wallet / merchant statement read) plus the matching company line.</summary>
        private void PostLines(SettlementVoucher voucher, SettlementOpenItemDto item, decimal amount, string actor)
        {
            var key = $"voucher:{voucher.SettlementVoucherId}:{item.Kind}:{item.OrderId?.ToString() ?? "float"}";
            var note = voucher.VoucherNo;

            (OrderJournalEntryKind partyKind, JournalDirection partyDirection, OrderJournalEntryKind companyKind, JournalDirection companyDirection) = item.Kind switch
            {
                SettlementAllocationKind.DeliveryCash => (OrderJournalEntryKind.DeliveryRemittanceToCompany, JournalDirection.Credit, OrderJournalEntryKind.CompanyCashReceivedFromDelivery, JournalDirection.Debit),
                SettlementAllocationKind.DeliveryLegacyFloat => (OrderJournalEntryKind.DeliveryCashFloatReturned, JournalDirection.Credit, OrderJournalEntryKind.CompanyCashReceivedFromDelivery, JournalDirection.Debit),
                SettlementAllocationKind.DeliveryCommission => (OrderJournalEntryKind.DeliveryPaidByCompany, JournalDirection.Debit, OrderJournalEntryKind.CompanyPaidDelivery, JournalDirection.Credit),
                SettlementAllocationKind.MerchantEarnings => (OrderJournalEntryKind.MerchantPaidByCompany, JournalDirection.Debit, OrderJournalEntryKind.CompanyPaidMerchant, JournalDirection.Credit),
                _ => throw new InvalidOperationException($"Unknown allocation kind {item.Kind}")
            };

            _context.OrderJournals.Add(OrderJournal.Create(
                item.OrderId, voucher.PartyType, voucher.PartyId, partyDirection, amount, partyKind,
                $"{key}:party", note, createdBy: actor, settlementVoucherId: voucher.SettlementVoucherId));

            _context.OrderJournals.Add(OrderJournal.Create(
                item.OrderId, LedgerPartyType.Company, null, companyDirection, amount, companyKind,
                $"{key}:company", note, createdBy: actor, settlementVoucherId: voucher.SettlementVoucherId));
        }

        private async Task NotifyPartyAsync(SettlementVoucher voucher, CancellationToken cancellationToken)
        {
            var title = voucher.Direction == SettlementDirection.CollectFromParty
                ? $"Payment received ({voucher.VoucherNo})"
                : $"Payment sent ({voucher.VoucherNo})";
            var message = voucher.Direction == SettlementDirection.CollectFromParty
                ? $"The company received {voucher.Amount:0.##} from you."
                : $"The company paid you {voucher.Amount:0.##}.";
            try
            {
                if (voucher.PartyType == LedgerPartyType.Delivery)
                    await _riderNotifier.NotifyAsync(voucher.PartyId, title, message, "settlement", cancellationToken: cancellationToken);
                else
                    await _merchantNotifications.SendToMerchantsAsync(new[] { voucher.PartyId }, title, message, NotificationType.OrderUpdated);
            }
            catch
            {
                // A failed notification must not undo a posted voucher.
            }
        }

        private Task<SettlementVoucherDetailDto?> FindByRequestAsync(Guid requestId, CancellationToken cancellationToken) =>
            SettlementVoucherReader.GetDetailAsync(_context, v => v.RequestId == requestId, cancellationToken);
    }
}
