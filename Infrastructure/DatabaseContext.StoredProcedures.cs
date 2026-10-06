using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Reflection;
using Infrastructure.StoredProcedures;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Infrastructure
{
    public partial class DatabaseContext
    {
        /// <summary>Name of the admin order detail procedure (created in migration <c>AdminOrderDetailProcedure</c>).</summary>
        public const string AdminOrderDetailProcedure = "dbo.usp_GetAdminOrderDetail";

        /// <summary>
        /// Loads everything the admin order page needs in one round trip.
        /// The procedure filters each table by <paramref name="orderId"/> first (CTEs) and only then joins
        /// the few lookup rows it needs, returning 12 result sets in a fixed order.
        /// </summary>
        public async Task<AdminOrderDetailResult> GetAdminOrderDetailAsync(
            int orderId,
            int cancellationFeeWalletType,
            CancellationToken cancellationToken = default)
        {
            var result = new AdminOrderDetailResult();
            var connection = Database.GetDbConnection();
            var openedHere = connection.State != ConnectionState.Open;
            if (openedHere)
                await connection.OpenAsync(cancellationToken);

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = AdminOrderDetailProcedure;
                command.CommandType = CommandType.StoredProcedure;
                command.Transaction = Database.CurrentTransaction?.GetDbTransaction();
                command.Parameters.Add(new SqlParameter("@OrderId", SqlDbType.Int) { Value = orderId });
                command.Parameters.Add(new SqlParameter("@CancellationFeeType", SqlDbType.Int) { Value = cancellationFeeWalletType });

                await using var reader = await command.ExecuteReaderAsync(CommandBehavior.Default, cancellationToken);

                result.Header = (await ReadRowsAsync<AdminOrderHeaderRow>(reader, cancellationToken)).FirstOrDefault();
                await reader.NextResultAsync(cancellationToken);
                result.Vehicles.AddRange(await ReadRowsAsync<AdminOrderVehicleRow>(reader, cancellationToken));
                await reader.NextResultAsync(cancellationToken);
                result.Payments.AddRange(await ReadRowsAsync<AdminOrderPaymentRow>(reader, cancellationToken));
                await reader.NextResultAsync(cancellationToken);
                result.Refund = (await ReadRowsAsync<AdminOrderRefundRow>(reader, cancellationToken)).FirstOrDefault();
                await reader.NextResultAsync(cancellationToken);
                result.Totals = (await ReadRowsAsync<AdminOrderTotalsRow>(reader, cancellationToken)).FirstOrDefault();
                await reader.NextResultAsync(cancellationToken);
                result.CancellationFee = (await ReadRowsAsync<AdminOrderCancellationFeeRow>(reader, cancellationToken)).FirstOrDefault();
                await reader.NextResultAsync(cancellationToken);
                result.MerchantOrders.AddRange(await ReadRowsAsync<AdminMerchantOrderRow>(reader, cancellationToken));
                await reader.NextResultAsync(cancellationToken);
                result.MerchantPayments.AddRange(await ReadRowsAsync<AdminMerchantPaymentRow>(reader, cancellationToken));
                await reader.NextResultAsync(cancellationToken);
                result.DeliveryLegs.AddRange(await ReadRowsAsync<AdminDeliveryLegRow>(reader, cancellationToken));
                await reader.NextResultAsync(cancellationToken);
                result.DeliveryPayments.AddRange(await ReadRowsAsync<AdminDeliveryPaymentRow>(reader, cancellationToken));
                await reader.NextResultAsync(cancellationToken);
                result.Journals.AddRange(await ReadRowsAsync<AdminJournalRow>(reader, cancellationToken));
                await reader.NextResultAsync(cancellationToken);
                result.HandoverImages.AddRange(await ReadRowsAsync<AdminHandoverImageRow>(reader, cancellationToken));
            }
            finally
            {
                if (openedHere)
                    await connection.CloseAsync();
            }

            return result;
        }

        // ── Lightweight column → property mapper for procedure result sets ─────────
        private static readonly ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>> RowProperties = new();

        private static async Task<List<T>> ReadRowsAsync<T>(DbDataReader reader, CancellationToken cancellationToken)
            where T : new()
        {
            var props = RowProperties.GetOrAdd(typeof(T), t => t
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite)
                .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase));

            // Resolve column ordinals once per result set.
            var map = new List<(int Ordinal, PropertyInfo Property, Type Target)>();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                if (props.TryGetValue(reader.GetName(i), out var prop))
                    map.Add((i, prop, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType));
            }

            var rows = new List<T>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new T();
                foreach (var (ordinal, prop, target) in map)
                {
                    if (reader.IsDBNull(ordinal))
                        continue;
                    var value = reader.GetValue(ordinal);
                    prop.SetValue(row, value.GetType() == target ? value : Convert.ChangeType(value, target));
                }
                rows.Add(row);
            }
            return rows;
        }
    }
}
