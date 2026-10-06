using Application.Features.Order.DTOs;
using CSharpFunctionalExtensions;
using Domain.Enums;
using Infrastructure;
using Infrastructure.Services;
using MediatR;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.Order.Query.GetOrderByIdQuery
{
    public record GetOrderByIdQuery : IRequest<Result<OrderDetailDto>>
    {
        public int OrderId { get; set; }
    }

    /// <summary>
    /// Admin order detail. One call to <c>dbo.usp_GetAdminOrderDetail</c> returns every section already
    /// filtered by order id (filter first, join after), replacing a wide Include graph plus nine follow-up queries.
    /// </summary>
    public class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, Result<OrderDetailDto>>
    {
        private readonly DatabaseContext _context;
        private readonly IImageService _imageService;

        public GetOrderByIdQueryHandler(DatabaseContext context, IImageService imageService)
        {
            _context = context;
            _imageService = imageService;
        }

        public async Task<Result<OrderDetailDto>> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
        {
            var data = await _context.GetAdminOrderDetailAsync(
                request.OrderId,
                (int)WalletType.OrderCancellationFees,
                cancellationToken);

            var order = data.Header;
            if (order == null)
            {
                return Result.Failure<OrderDetailDto>($"Order with ID {request.OrderId} not found");
            }

            var orderVehicles = data.Vehicles.Select(v => new OrderVehicleDto
            {
                VehicleId = v.VehicleId,
                VehicleName = v.VehicleName ?? string.Empty,
                VehicleCode = v.VehicleCode ?? string.Empty,
                ImageUrl = !string.IsNullOrWhiteSpace(v.ImageUrl) ? _imageService.GetImageUrl(v.ImageUrl) : null,
                MerchantId = v.MerchantId,
                MerchantName = v.MerchantName ?? string.Empty,
                Status = ParseVehicleStatus(v.Status),
                Color = v.Color ?? string.Empty,
                Type = v.Type ?? string.Empty,
                Model = v.Model ?? string.Empty,
                Price = v.Price,
                DeliveryFee = v.DeliveryFee,
                MerchantCashOnReceive = v.MerchantCashOnReceive ?? false,
                MerchantZoneId = v.MerchantZoneId ?? 0,
                MerchantZoneName = v.MerchantZoneName ?? string.Empty,
                SpeedKmh = v.SpeedKmh,
                EngineCapacityCc = v.EngineCapacityCc,
                ReceivedFromOwner = v.ReceivedFromOwner,
                ReceivedFromOwnerAt = v.ReceivedFromOwnerAt,
                ReceivedFromOwnerImageUrl = _imageService.GetImageUrl(v.ReceivedFromOwnerImageUrl),
                DeliveredToCustomer = v.DeliveredToCustomer,
                DeliveredToCustomerAt = v.DeliveredToCustomerAt,
                DeliveredToCustomerImageUrl = _imageService.GetImageUrl(v.DeliveredToCustomerImageUrl),
                ReceivedFromCustomer = v.ReceivedFromCustomer,
                ReceivedFromCustomerAt = v.ReceivedFromCustomerAt,
                ReceivedFromCustomerImageUrl = _imageService.GetImageUrl(v.ReceivedFromCustomerImageUrl),
                DeliveredToOwner = v.DeliveredToOwner,
                DeliveredToOwnerAt = v.DeliveredToOwnerAt,
                DeliveredToOwnerImageUrl = _imageService.GetImageUrl(v.DeliveredToOwnerImageUrl),
                DeliveryFailed = v.DeliveryFailed,
                DeliveryFailureReason = v.DeliveryFailureReason,
                DeliveryFailureFaultParty = (FaultParty?)v.DeliveryFailureFaultParty,
                MerchantResponseStatus = (MerchantVehicleResponseStatus)v.MerchantResponseStatus
            }).ToList();

            var orderDetailDto = new OrderDetailDto
            {
                OrderId = order.OrderId,
                OrderCode = order.OrderCode,
                CustomerId = order.CustomerId,
                CustomerName = order.CustomerName ?? string.Empty,
                CustomerMobileNumber = order.CustomerMobileNumber ?? string.Empty,
                SubCategoryId = order.SubCategoryId,
                SubCategoryName = order.SubCategoryName ?? string.Empty,
                CityId = order.CityId,
                CityName = order.CityName ?? string.Empty,
                DestinationZoneId = order.DestinationZoneId,
                DestinationZoneName = order.DestinationZoneName ?? string.Empty,
                ReservationDateFrom = order.ReservationDateFrom,
                ReservationDateTo = order.ReservationDateTo,
                VehiclesCount = order.VehiclesCount,
                OrderSubTotal = order.OrderSubTotal,
                OrderTotal = order.OrderTotal,
                PreviousDebt = order.PreviousDebt,
                MoneyRefunded = order.MoneyRefunded,
                Notes = order.Notes,
                PassportImage = Domain.Models.Order.NormalizePassportImage(order.PassportImage),
                HotelName = order.HotelName ?? string.Empty,
                HotelAddress = order.HotelAddress ?? string.Empty,
                HotelPhone = order.HotelPhone,
                IsUrgent = order.IsUrgent,
                PaymentMethod = (PaymentMethod)order.PaymentMethodId,
                OrderState = (OrderState)order.OrderState,
                CreatedDate = order.CreatedDate,
                ReceiptFaultParty = (FaultParty?)order.ReceiptFaultParty,
                ReceiptRejectNote = order.ReceiptRejectNote,
                OrderTotalDebitedToCompany = order.OrderTotalDebitedToCompany,
                OrderDeliveryFailed = order.OrderDeliveryFailed,
                OrderDeliveryFailureReason = order.OrderDeliveryFailureReason,
                OrderDeliveryFailureFaultParty = (FaultParty?)order.OrderDeliveryFailureFaultParty,
                OrderVehicles = orderVehicles,
                OrderPayments = data.Payments.Select(p => new OrderPaymentDto
                {
                    Id = p.Id,
                    OrderId = p.OrderId,
                    PaymentMethod = (PaymentMethod)p.PaymentMethodId,
                    Total = p.Total,
                    State = (PaymentState)p.State,
                    CreatedDate = p.CreatedDate
                }).ToList(),
                RefundablePaypalAmount = data.Refund == null ? null : new RefundablePaypalAmountDto
                {
                    Id = data.Refund.Id,
                    CustomerId = data.Refund.CustomerId,
                    OrderId = data.Refund.OrderId,
                    OrderTotal = data.Refund.OrderTotal,
                    CancellationFees = data.Refund.CancellationFees,
                    RefundableAmount = data.Refund.RefundableAmount,
                    State = (RefundState)data.Refund.State,
                    CreatedDate = data.Refund.CreatedDate
                },
                OrderTotals = data.Totals == null ? null : new OrderTotalsDto
                {
                    Id = data.Totals.Id,
                    OrderId = data.Totals.OrderId,
                    SubTotal = data.Totals.SubTotal,
                    ServiceFees = data.Totals.ServiceFees,
                    DeliveryFees = data.Totals.DeliveryFees,
                    UrgentFees = data.Totals.UrgentFees,
                    TieredDiscount = data.Totals.TieredDiscount,
                    TotalAfterAllFees = data.Totals.TotalAfterAllFees
                },
                OrderCancellationFee = data.CancellationFee == null ? null : new OrderCancellationFeeInfoDto
                {
                    WalletEntryId = data.CancellationFee.Id,
                    Amount = data.CancellationFee.Withdraw,
                    State = (CustomerWalletState)data.CancellationFee.State
                },
                MerchantOrders = data.MerchantOrders.Select(mo =>
                {
                    // Counts come from the vehicle rows already loaded — no extra query.
                    var merchantVehicles = orderVehicles.Where(v => v.MerchantId == mo.MerchantId).ToList();
                    return new MerchantOrderDto
                    {
                        MerchantOrderId = mo.MerchantOrderId,
                        OrderId = mo.OrderId,
                        MerchantId = mo.MerchantId,
                        MerchantName = mo.MerchantName ?? string.Empty,
                        ResponseStatus = (MerchantOrderResponseStatus)mo.ResponseStatus,
                        RejectReason = mo.RejectReason,
                        RespondedAt = mo.RespondedAt,
                        CreatedDate = mo.CreatedDate,
                        ConfirmedVehiclesCount = merchantVehicles.Count(v => v.MerchantResponseStatus == MerchantVehicleResponseStatus.Confirmed),
                        DeclinedVehiclesCount = merchantVehicles.Count(v => v.MerchantResponseStatus == MerchantVehicleResponseStatus.Declined),
                        PendingVehiclesCount = merchantVehicles.Count(v => v.MerchantResponseStatus == MerchantVehicleResponseStatus.Pending),
                        DeclinedVehicleCodes = merchantVehicles
                            .Where(v => v.MerchantResponseStatus == MerchantVehicleResponseStatus.Declined)
                            .Select(v => v.VehicleCode)
                            .ToList()
                    };
                }).ToList(),
                MerchantOrderPaymentDetails = data.MerchantPayments.Select(p => new MerchantOrderPaymentDetailDto
                {
                    MerchantOrderPaymentDetailId = p.MerchantOrderPaymentDetailId,
                    OrderId = p.OrderId,
                    MerchantId = p.MerchantId,
                    MerchantName = p.MerchantName ?? string.Empty,
                    VehicleId = p.VehicleId,
                    VehicleCode = p.VehicleCode ?? string.Empty,
                    VehicleRental = p.VehicleRental,
                    NetAmount = p.NetAmount,
                    CompanyCommissionPercent = p.CompanyCommissionPercent,
                    CompanyCommissionAmount = p.CompanyCommissionAmount
                }).ToList(),
                DeliveryMenOrders = data.DeliveryLegs.Select(d => new DeliveryMenOrderDto
                {
                    DeliveryMenOrderId = d.DeliveryMenOrderId,
                    OrderId = d.OrderId,
                    VehicleId = d.VehicleId,
                    VehicleCode = d.VehicleCode ?? string.Empty,
                    DeliveryId = d.DeliveryId,
                    DeliveryName = d.DeliveryName ?? string.Empty,
                    DeliveryReceivedFromMerchant = d.DeliveryReceivedFromMerchant,
                    ReceivedFromMerchantAt = d.ReceivedFromMerchantAt,
                    Leg = (DeliveryLeg)d.Leg
                }).ToList(),
                DeliveryOrderPaymentDetails = data.DeliveryPayments.Select(d => new DeliveryOrderPaymentDetailDto
                {
                    DeliveryOrderPaymentDetailId = d.DeliveryOrderPaymentDetailId,
                    OrderId = d.OrderId,
                    DeliveryId = d.DeliveryId,
                    DeliveryName = d.DeliveryName ?? string.Empty,
                    VehicleId = d.VehicleId,
                    VehicleCode = d.VehicleCode ?? string.Empty,
                    DeliveryFeeShare = d.DeliveryFeeShare,
                    Leg = (DeliveryLeg)d.Leg,
                    CommissionPercent = d.CommissionPercent
                }).ToList(),
                OrderJournals = data.Journals.Select(j => new OrderJournalDto
                {
                    OrderJournalId = j.OrderJournalId,
                    OrderId = j.OrderId,
                    VehicleId = j.VehicleId,
                    VehicleCode = j.VehicleCode,
                    PartyType = (LedgerPartyType)j.PartyType,
                    PartyId = j.PartyId,
                    Direction = (JournalDirection)j.Direction,
                    Amount = j.Amount,
                    EntryKind = (OrderJournalEntryKind)j.EntryKind,
                    IdempotencyKey = j.IdempotencyKey,
                    FaultParty = (FaultParty?)j.FaultParty,
                    Note = j.Note,
                    CreatedBy = j.CreatedBy,
                    CreatedDate = j.CreatedDate
                }).ToList(),
                HandoverImages = data.HandoverImages.Select(i => new OrderVehicleHandoverImageDto
                {
                    VehicleId = i.VehicleId,
                    Step = (HandoverStep)i.Step,
                    Position = (HandoverImagePosition)i.Position,
                    ImageUrl = i.ImageUrl ?? string.Empty,
                    DeliveryId = i.DeliveryId,
                    CreatedDate = i.CreatedDate
                }).ToList()
            };

            return Result.Success(orderDetailDto);
        }

        /// <summary>Vehicle.Status is stored as the enum name (string conversion).</summary>
        private static int ParseVehicleStatus(string? status) =>
            Enum.TryParse<VehicleStatus>(status, ignoreCase: true, out var parsed) ? (int)parsed : 0;
    }
}
