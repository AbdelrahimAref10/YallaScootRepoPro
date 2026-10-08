/** Multi-select filters a report can offer (the date range is always shown). */
export type ReportFilterKey =
  | 'cities'
  | 'merchants'
  | 'deliveries'
  | 'customers'
  | 'vehicles'
  | 'orderStates'
  | 'paymentMethods'
  | 'paymentStates'
  | 'refundStates'
  | 'directions'
  | 'partyTypes';

export type ReportScopeName = 'admin' | 'merchant';

export interface ReportDefinition {
  key: string;
  titleKey: string;
  descriptionKey: string;
  groupKey: string;
  filters: ReportFilterKey[];
}

export const ADMIN_REPORTS: ReportDefinition[] = [
  { key: 'merchant-balances', titleKey: 'reports.def.merchantBalances', descriptionKey: 'reports.def.merchantBalancesDesc', groupKey: 'reports.group.balances', filters: ['merchants', 'cities'] },
  { key: 'delivery-balances', titleKey: 'reports.def.deliveryBalances', descriptionKey: 'reports.def.deliveryBalancesDesc', groupKey: 'reports.group.balances', filters: ['deliveries', 'cities'] },
  { key: 'customer-balances', titleKey: 'reports.def.customerBalances', descriptionKey: 'reports.def.customerBalancesDesc', groupKey: 'reports.group.balances', filters: ['customers', 'cities'] },
  { key: 'statement', titleKey: 'reports.def.statement', descriptionKey: 'reports.def.statementDesc', groupKey: 'reports.group.balances', filters: ['partyTypes', 'merchants', 'deliveries', 'cities'] },
  { key: 'orders', titleKey: 'reports.def.orders', descriptionKey: 'reports.def.ordersDesc', groupKey: 'reports.group.operations', filters: ['cities', 'customers', 'merchants', 'deliveries', 'orderStates', 'paymentMethods'] },
  { key: 'payments', titleKey: 'reports.def.payments', descriptionKey: 'reports.def.paymentsDesc', groupKey: 'reports.group.cash', filters: ['cities', 'customers', 'paymentMethods', 'paymentStates'] },
  { key: 'vouchers', titleKey: 'reports.def.vouchers', descriptionKey: 'reports.def.vouchersDesc', groupKey: 'reports.group.cash', filters: ['directions', 'partyTypes', 'merchants', 'deliveries'] },
  { key: 'treasury', titleKey: 'reports.def.treasury', descriptionKey: 'reports.def.treasuryDesc', groupKey: 'reports.group.cash', filters: [] },
  { key: 'refunds', titleKey: 'reports.def.refunds', descriptionKey: 'reports.def.refundsDesc', groupKey: 'reports.group.cash', filters: ['cities', 'customers', 'refundStates'] }
];

export const MERCHANT_REPORTS: ReportDefinition[] = [
  { key: 'statement', titleKey: 'reports.def.myStatement', descriptionKey: 'reports.def.myStatementDesc', groupKey: 'reports.group.balances', filters: [] },
  { key: 'orders', titleKey: 'reports.def.myOrders', descriptionKey: 'reports.def.myOrdersDesc', groupKey: 'reports.group.operations', filters: ['orderStates', 'vehicles'] },
  { key: 'order-vehicles', titleKey: 'reports.def.myOrderVehicles', descriptionKey: 'reports.def.myOrderVehiclesDesc', groupKey: 'reports.group.operations', filters: ['orderStates', 'vehicles'] },
  { key: 'payments', titleKey: 'reports.def.myPayments', descriptionKey: 'reports.def.myPaymentsDesc', groupKey: 'reports.group.cash', filters: [] },
  { key: 'vehicles', titleKey: 'reports.def.myVehicles', descriptionKey: 'reports.def.myVehiclesDesc', groupKey: 'reports.group.operations', filters: ['vehicles'] }
];

export function reportsFor(scope: ReportScopeName): ReportDefinition[] {
  return scope === 'merchant' ? MERCHANT_REPORTS : ADMIN_REPORTS;
}

export function reportsBaseRoute(scope: ReportScopeName): string {
  return scope === 'merchant' ? '/merchant/reports' : '/main/reports';
}
