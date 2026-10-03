using CSharpFunctionalExtensions;
using Domain.Common;
using Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Delivery.Common
{
    /// <summary>Resolves the rider behind the current request (same pattern as merchants: by UserId).</summary>
    public static class RiderContext
    {
        public static async Task<Result<Domain.Models.Delivery>> GetCurrentAsync(
            DatabaseContext context,
            IUserSession userSession,
            CancellationToken cancellationToken,
            bool tracking = false)
        {
            if (!userSession.IsAuthenticated || userSession.UserId <= 0)
                return Result.Failure<Domain.Models.Delivery>("User is not authenticated");

            var query = tracking ? context.Deliveries.AsTracking() : context.Deliveries.AsNoTracking();
            var delivery = await query.FirstOrDefaultAsync(
                d => d.UserId == userSession.UserId && !d.IsDeleted,
                cancellationToken);

            if (delivery == null)
                return Result.Failure<Domain.Models.Delivery>("Delivery profile not found for the current user");
            if (!delivery.IsActive)
                return Result.Failure<Domain.Models.Delivery>("Delivery account is not active");

            return Result.Success(delivery);
        }
    }
}
