using Domain.Models;
using Infrastructure;

namespace Application.Common
{
    public static class MerchantLookup
    {
        /// <summary>The merchant <paramref name="userId"/> works for, as its owner or as active staff.</summary>
        public static IQueryable<Merchant> OfUser(this IQueryable<Merchant> merchants, DatabaseContext context, int userId) =>
            merchants.Where(m => context.MerchantUsers.Any(mu =>
                mu.MerchantId == m.MerchantId && mu.UserId == userId && mu.IsActive && !mu.IsDeleted));
    }
}
