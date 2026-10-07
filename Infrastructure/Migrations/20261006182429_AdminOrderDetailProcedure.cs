using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <summary>
    /// Creates <c>dbo.usp_GetAdminOrderDetail</c>: the admin order page in one round trip.
    /// Every section filters its own table by @OrderId first (index seek on OrderId) inside a CTE,
    /// and only then joins the handful of lookup rows it needs by primary key. Nothing is joined
    /// before it is filtered, so no row multiplication across collections.
    /// Result sets (fixed order, read by DatabaseContext.GetAdminOrderDetailAsync):
    ///  1 header · 2 vehicles · 3 payments · 4 PayPal refund · 5 totals · 6 cancellation fee
    ///  7 merchant invitations · 8 merchant payouts · 9 rider legs · 10 rider payouts · 11 journal · 12 handover photos
    /// </summary>
    public partial class AdminOrderDetailProcedure : Migration
    {
        /// <summary>Procedure text as created here; later migrations alter it from this.</summary>
        internal const string ProcedureSql = @"
CREATE OR ALTER PROCEDURE dbo.usp_GetAdminOrderDetail
    @OrderId INT,
    @CancellationFeeType INT
AS
BEGIN
    SET NOCOUNT ON;

    -- Wallet lines are matched by order code text, so resolve the code once.
    DECLARE @Marker NVARCHAR(120) =
        (SELECT N'Order #' + o.OrderCode FROM dbo.VO_Order o WHERE o.OrderId = @OrderId);

    -- 1) Header: the single order row, then its small parents by primary key.
    ;WITH ord AS (
        SELECT o.OrderId, o.OrderCode, o.CustomerId, o.SubCategoryId, o.CityId, o.DestinationZoneId,
               o.ReservationDateFrom, o.ReservationDateTo, o.VehiclesCount, o.OrderSubTotal, o.OrderTotal,
               o.PreviousDebt, o.MoneyRefunded, o.Notes, o.PassportImage, o.HotelName, o.HotelAddress,
               o.HotelPhone, o.IsUrgent, o.PaymentMethodId, o.OrderState, o.CreatedDate, o.ReceiptFaultParty,
               o.ReceiptRejectNote, o.OrderTotalDebitedToCompany, o.OrderDeliveryFailed,
               o.OrderDeliveryFailureReason, o.OrderDeliveryFailureFaultParty
        FROM dbo.VO_Order o
        WHERE o.OrderId = @OrderId
    )
    SELECT ord.*,
           c.FullName      AS CustomerName,
           c.MobileNumber  AS CustomerMobileNumber,
           sc.Name         AS SubCategoryName,
           ci.Name         AS CityName,
           z.Name          AS DestinationZoneName
    FROM ord
    LEFT JOIN dbo.VO_Customer    c  ON c.CustomerId     = ord.CustomerId
    LEFT JOIN dbo.VO_SubCategory sc ON sc.SubCategoryId = ord.SubCategoryId
    LEFT JOIN dbo.VO_City        ci ON ci.CityId        = ord.CityId
    LEFT JOIN dbo.VO_Zone        z  ON z.ZoneId         = ord.DestinationZoneId;

    -- 2) Vehicles on the order, with vehicle, merchant and merchant-zone lookups.
    ;WITH ov AS (
        SELECT x.VehicleId, x.DeliveryFee, x.CreatedDate,
               x.ReceivedFromOwner, x.ReceivedFromOwnerAt, x.ReceivedFromOwnerImageUrl,
               x.DeliveredToCustomer, x.DeliveredToCustomerAt, x.DeliveredToCustomerImageUrl,
               x.ReceivedFromCustomer, x.ReceivedFromCustomerAt, x.ReceivedFromCustomerImageUrl,
               x.DeliveredToOwner, x.DeliveredToOwnerAt, x.DeliveredToOwnerImageUrl,
               x.DeliveryFailed, x.DeliveryFailureReason, x.DeliveryFailureFaultParty, x.MerchantResponseStatus
        FROM dbo.VO_OrderVehicle x
        WHERE x.OrderId = @OrderId
    )
    SELECT ov.VehicleId,
           v.Name AS VehicleName, v.VehicleCode, v.ImageUrl, v.MerchantId, v.Status, v.Color, v.Type,
           v.Model, v.Price, v.SpeedKmh, v.EngineCapacityCc,
           m.FullName AS MerchantName, m.CashOnReceive AS MerchantCashOnReceive,
           m.ZoneId AS MerchantZoneId, mz.Name AS MerchantZoneName,
           ov.DeliveryFee,
           ov.ReceivedFromOwner, ov.ReceivedFromOwnerAt, ov.ReceivedFromOwnerImageUrl,
           ov.DeliveredToCustomer, ov.DeliveredToCustomerAt, ov.DeliveredToCustomerImageUrl,
           ov.ReceivedFromCustomer, ov.ReceivedFromCustomerAt, ov.ReceivedFromCustomerImageUrl,
           ov.DeliveredToOwner, ov.DeliveredToOwnerAt, ov.DeliveredToOwnerImageUrl,
           ov.DeliveryFailed, ov.DeliveryFailureReason, ov.DeliveryFailureFaultParty, ov.MerchantResponseStatus
    FROM ov
    INNER JOIN dbo.VO_Vehicle  v  ON v.VehicleId  = ov.VehicleId
    LEFT  JOIN dbo.VO_Merchant m  ON m.MerchantId = v.MerchantId
    LEFT  JOIN dbo.VO_Zone     mz ON mz.ZoneId    = m.ZoneId
    ORDER BY ov.CreatedDate, ov.VehicleId;

    -- 3) Payments.
    SELECT p.Id, p.OrderId, p.PaymentMethodId, p.Total, p.State, p.CreatedDate
    FROM dbo.VO_OrderPayment p
    WHERE p.OrderId = @OrderId
    ORDER BY p.CreatedDate, p.Id;

    -- 4) PayPal refund (at most one).
    SELECT TOP (1) r.Id, r.CustomerId, r.OrderId, r.OrderTotal, r.CancellationFees, r.RefundableAmount, r.State, r.CreatedDate
    FROM dbo.VO_RefundablePaypalAmount r
    WHERE r.OrderId = @OrderId
    ORDER BY r.Id;

    -- 5) Price breakdown (at most one).
    SELECT TOP (1) t.Id, t.OrderId, t.SubTotal, t.ServiceFees, t.DeliveryFees, t.UrgentFees, t.TieredDiscount, t.TotalAfterAllFees
    FROM dbo.OrderTotals t
    WHERE t.OrderId = @OrderId
    ORDER BY t.Id;

    -- 6) Cancellation fee wallet line: OrderId seek first, then the cheap type/text checks.
    ;WITH w AS (
        SELECT cw.Id, cw.Withdraw, cw.State, cw.Type, cw.Description
        FROM dbo.VO_CustomerWallet cw
        WHERE cw.OrderId = @OrderId
    )
    SELECT TOP (1) w.Id, w.Withdraw, w.State
    FROM w
    WHERE w.Type = @CancellationFeeType
      AND @Marker IS NOT NULL
      AND CHARINDEX(@Marker, w.Description) > 0
    ORDER BY w.Id;

    -- 7) Merchant invitations.
    ;WITH mo AS (
        SELECT x.MerchantOrderId, x.OrderId, x.MerchantId, x.ResponseStatus, x.RejectReason, x.RespondedAt, x.CreatedDate
        FROM dbo.VO_MerchantOrder x
        WHERE x.OrderId = @OrderId
    )
    SELECT mo.*, m.FullName AS MerchantName
    FROM mo
    LEFT JOIN dbo.VO_Merchant m ON m.MerchantId = mo.MerchantId
    ORDER BY mo.CreatedDate, mo.MerchantOrderId;

    -- 8) Merchant payout snapshots.
    ;WITH mp AS (
        SELECT x.MerchantOrderPaymentDetailId, x.OrderId, x.MerchantId, x.VehicleId, x.VehicleRental,
               x.NetAmount, x.CompanyCommissionPercent, x.CompanyCommissionAmount
        FROM dbo.VO_MerchantOrderPaymentDetail x
        WHERE x.OrderId = @OrderId
    )
    SELECT mp.*, m.FullName AS MerchantName, v.VehicleCode
    FROM mp
    LEFT JOIN dbo.VO_Merchant m ON m.MerchantId = mp.MerchantId
    LEFT JOIN dbo.VO_Vehicle  v ON v.VehicleId  = mp.VehicleId
    ORDER BY mp.MerchantOrderPaymentDetailId;

    -- 9) Rider legs (delivery / return per vehicle).
    ;WITH dl AS (
        SELECT x.DeliveryMenOrderId, x.OrderId, x.VehicleId, x.DeliveryId, x.DeliveryReceivedFromMerchant,
               x.ReceivedFromMerchantAt, x.Leg
        FROM dbo.VO_DeliveryMenOrder x
        WHERE x.OrderId = @OrderId
    )
    SELECT dl.*, d.FullName AS DeliveryName, v.VehicleCode
    FROM dl
    LEFT JOIN dbo.VO_Delivery d ON d.DeliveryId = dl.DeliveryId
    LEFT JOIN dbo.VO_Vehicle  v ON v.VehicleId  = dl.VehicleId
    ORDER BY dl.DeliveryMenOrderId;

    -- 10) Rider payout snapshots.
    ;WITH dp AS (
        SELECT x.DeliveryOrderPaymentDetailId, x.OrderId, x.DeliveryId, x.VehicleId, x.DeliveryFeeShare,
               x.Leg, x.CommissionPercent
        FROM dbo.VO_DeliveryOrderPaymentDetail x
        WHERE x.OrderId = @OrderId
    )
    SELECT dp.*, d.FullName AS DeliveryName, v.VehicleCode
    FROM dp
    LEFT JOIN dbo.VO_Delivery d ON d.DeliveryId = dp.DeliveryId
    LEFT JOIN dbo.VO_Vehicle  v ON v.VehicleId  = dp.VehicleId
    ORDER BY dp.DeliveryOrderPaymentDetailId;

    -- 11) Journal entries.
    ;WITH j AS (
        SELECT x.OrderJournalId, x.OrderId, x.VehicleId, x.PartyType, x.PartyId, x.Direction, x.Amount,
               x.EntryKind, x.IdempotencyKey, x.FaultParty, x.Note, x.CreatedBy, x.CreatedDate
        FROM dbo.VO_OrderJournal x
        WHERE x.OrderId = @OrderId
    )
    SELECT j.*, v.VehicleCode
    FROM j
    LEFT JOIN dbo.VO_Vehicle v ON v.VehicleId = j.VehicleId
    ORDER BY j.CreatedDate, j.OrderJournalId;

    -- 12) Handover photos.
    SELECT i.VehicleId, i.Step, i.Position, i.ImageUrl, i.DeliveryId, i.CreatedDate
    FROM dbo.VO_OrderVehicleHandoverImage i
    WHERE i.OrderId = @OrderId
    ORDER BY i.VehicleId, i.Step, i.Position;
END
";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ProcedureSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_GetAdminOrderDetail;");
        }
    }
}
