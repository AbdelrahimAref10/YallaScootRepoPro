import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { Observable } from 'rxjs';
import {
  AdminCustomerClient,
  AdminReportClient,
  FileResponse,
  LedgerPartyType,
  MerchantReportClient,
  MerchantVehicleClient,
  OrderState,
  PaymentMethod,
  PaymentState,
  RefundState,
  ReportColumnDto,
  ReportColumnType,
  ReportExportFormat,
  ReportKpiDto,
  ReportResultDto,
  SettlementDirection
} from '../../../core/services/clientAPI';
import { LocaleService } from '../../../core/services/locale.service';
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';
import { MultiSelectComponent, MultiSelectOption } from '../../../shared/components/multi-select/multi-select.component';
import { downloadFileResponse, emptyToNull, optionalDate, toNumberArray } from '../report-utils';
import { ReportDefinition, ReportFilterKey, ReportScopeName, reportsBaseRoute, reportsFor } from '../reports.config';
import { LookupService } from '../../../core/services/lookup.service';

type Row = Record<string, unknown>;

interface FilterField {
  key: ReportFilterKey;
  labelKey: string;
}

const FILTER_LABELS: Record<ReportFilterKey, string> = {
  cities: 'reports.filter.cities',
  merchants: 'reports.filter.merchants',
  deliveries: 'reports.filter.deliveries',
  customers: 'reports.filter.customers',
  vehicles: 'reports.filter.vehicles',
  orderStates: 'reports.filter.orderStates',
  paymentMethods: 'reports.filter.paymentMethods',
  paymentStates: 'reports.filter.paymentStates',
  refundStates: 'reports.filter.refundStates',
  directions: 'reports.filter.directions',
  partyTypes: 'reports.filter.partyTypes'
};

const OK_VALUES = new Set(['Completed', 'Paid', 'RefundSuccess', 'Available', 'CustomerReceived']);
const BAD_VALUES = new Set(['Cancelled', 'Failed', 'RefundFailed', 'CustomerRejectedReceipt', 'UnderMaintenance']);
const WARN_VALUES = new Set(['Pending', 'MerchantPending', 'RefundPending', 'CollectFromParty']);

@Component({
  selector: 'app-report-viewer',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, TranslatePipe, MultiSelectComponent],
  templateUrl: './report-viewer.component.html',
  styleUrls: ['../report-page-shared.css', './report-viewer.component.css']
})
export class ReportViewerComponent implements OnInit {
  private readonly lookups = inject(LookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly locale = inject(LocaleService);
  private readonly adminReports = inject(AdminReportClient);
  private readonly merchantReports = inject(MerchantReportClient);
  private readonly customerClient = inject(AdminCustomerClient);
  private readonly vehicleClient = inject(MerchantVehicleClient);

  readonly ColumnType = ReportColumnType;
  readonly ExportFormat = ReportExportFormat;

  scope: ReportScopeName = 'admin';
  definition: ReportDefinition | null = null;
  filterFields: FilterField[] = [];

  report: ReportResultDto | null = null;
  visibleRows: Row[] = [];
  isLoading = false;
  isExporting = false;
  errorMessage = '';

  fromDate = '';
  toDate = '';
  search = '';
  selected: Partial<Record<ReportFilterKey, number[]>> = {};
  options: Partial<Record<ReportFilterKey, MultiSelectOption[]>> = {};

  ngOnInit(): void {
    this.scope = this.route.snapshot.data['scope'] === 'merchant' ? 'merchant' : 'admin';
    this.route.paramMap.subscribe(params => {
      const key = params.get('key') ?? '';
      this.definition = reportsFor(this.scope).find(r => r.key === key) ?? null;
      if (!this.definition) {
        this.back();
        return;
      }
      this.filterFields = this.definition.filters.map(f => ({ key: f, labelKey: FILTER_LABELS[f] }));
      this.selected = {};
      this.report = null;
      this.visibleRows = [];
      this.search = '';
      this.loadOptions();
      this.run();
    });
  }

  back(): void {
    this.router.navigate([reportsBaseRoute(this.scope)]);
  }

  onFilterChange(key: ReportFilterKey, values: Array<string | number | boolean>): void {
    this.selected = { ...this.selected, [key]: toNumberArray(values) };
  }

  clearFilters(): void {
    this.selected = {};
    this.fromDate = '';
    this.toDate = '';
    this.search = '';
    this.run();
  }

  run(): void {
    if (!this.definition) return;
    if (this.fromDate && this.toDate && this.fromDate > this.toDate) {
      this.errorMessage = this.locale.translate('reports.invalidRange');
      return;
    }
    this.isLoading = true;
    this.errorMessage = '';
    this.request(false).subscribe({
      next: (result) => {
        this.report = result as ReportResultDto;
        this.applySearch();
        this.isLoading = false;
      },
      error: (err) => {
        this.errorMessage = err?.result?.errorMessage || err?.message || this.locale.translate('reports.loadFailed');
        this.isLoading = false;
      }
    });
  }

  export(format: ReportExportFormat): void {
    if (!this.definition) return;
    this.isExporting = true;
    this.request(true, format).subscribe({
      next: (file) => {
        const ext = format === ReportExportFormat.Pdf ? 'pdf' : 'xlsx';
        downloadFileResponse(file as FileResponse, `${this.definition!.key}.${ext}`);
        this.isExporting = false;
      },
      error: (err) => {
        this.errorMessage = err?.result?.errorMessage || err?.message || this.locale.translate('reports.exportFailed');
        this.isExporting = false;
      }
    });
  }

  applySearch(): void {
    const rows = (this.report?.rows ?? []) as Row[];
    const term = this.search.trim().toLowerCase();
    if (!term) {
      this.visibleRows = rows;
      return;
    }
    const textKeys = (this.report?.columns ?? [])
      .filter(c => c.type === ReportColumnType.Text)
      .map(c => c.key);
    this.visibleRows = rows.filter(row =>
      textKeys.some(k => String(row[k] ?? '').toLowerCase().includes(term)));
  }

  /** Totals follow the search so the footer matches what is on screen. */
  get footerTotals(): Record<string, number> {
    const totals: Record<string, number> = {};
    for (const col of this.report?.columns ?? []) {
      if (!col.total) continue;
      totals[col.key] = this.visibleRows.reduce((sum, row) => sum + (Number(row[col.key]) || 0), 0);
    }
    return totals;
  }

  get hasTotals(): boolean {
    return (this.report?.columns ?? []).some(c => c.total);
  }

  columnLabel(col: ReportColumnDto): string {
    return this.labelFor(col.labelKey, col.label);
  }

  kpiLabel(kpi: ReportKpiDto): string {
    return this.labelFor(kpi.labelKey, kpi.label);
  }

  isNumeric(col: ReportColumnDto): boolean {
    return col.type === ReportColumnType.Money || col.type === ReportColumnType.Number;
  }

  isOpeningRow(row: Row): boolean {
    return row['entry'] === 'OpeningBalance';
  }

  badgeLabel(col: ReportColumnDto, value: unknown): string {
    if (value === null || value === undefined || value === '') return '—';
    const key = `${col.valueKeyPrefix ?? 'reports.value.'}${value}`;
    const translated = this.locale.translate(key);
    return translated === key ? String(value) : translated;
  }

  badgeClass(value: unknown): string {
    const v = String(value ?? '');
    if (OK_VALUES.has(v)) return 'rp__chip--ok';
    if (BAD_VALUES.has(v)) return 'rp__chip--bad';
    if (WARN_VALUES.has(v)) return 'rp__chip--warn';
    return 'rp__chip--info';
  }

  formatMoney(value: unknown): string {
    const n = Number(value) || 0;
    return n.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }

  formatNumber(value: unknown): string {
    return (Number(value) || 0).toLocaleString();
  }

  formatKpi(kpi: ReportKpiDto): string {
    return kpi.type === ReportColumnType.Number ? this.formatNumber(kpi.value) : this.formatMoney(kpi.value);
  }

  moneyClass(col: ReportColumnDto, value: unknown): string {
    if (col.type !== ReportColumnType.Money) return '';
    const n = Number(value) || 0;
    if (n < 0) return 'rv__neg';
    return '';
  }


  private labelFor(labelKey: string, fallback: string): string {
    // English keeps the report's own (more specific) wording; other languages use the translation.
    if (this.locale.locale() === 'en') return fallback;
    const translated = this.locale.translate(labelKey);
    return translated === labelKey ? fallback : translated;
  }

  private request(exportFile: boolean, format?: ReportExportFormat): Observable<unknown> {
    const key = this.definition!.key;
    const s = this.selected;
    const args = [
      key,
      optionalDate(this.fromDate),
      optionalDate(this.toDate),
      emptyToNull(s.cities),
      emptyToNull(s.merchants),
      emptyToNull(s.deliveries),
      emptyToNull(s.customers),
      emptyToNull(s.vehicles),
      emptyToNull(s.orderStates) as OrderState[] | null,
      emptyToNull(s.paymentMethods) as PaymentMethod[] | null,
      emptyToNull(s.paymentStates) as PaymentState[] | null,
      emptyToNull(s.refundStates) as RefundState[] | null,
      emptyToNull(s.directions) as SettlementDirection[] | null,
      emptyToNull(s.partyTypes) as LedgerPartyType[] | null
    ] as const;

    if (this.scope === 'merchant') {
      return exportFile ? this.merchantReports.export(...args, format) : this.merchantReports.run(...args);
    }
    return exportFile ? this.adminReports.export(...args, format) : this.adminReports.run(...args);
  }

  private loadOptions(): void {
    const t = (k: string) => this.locale.translate(k);
    const enumOptions = (prefix: string, values: Record<string, string | number>): MultiSelectOption[] =>
      Object.entries(values)
        .filter(([, v]) => typeof v === 'number')
        .map(([name, v]) => {
          const key = `${prefix}${name}`;
          const label = t(key);
          return { value: v as number, label: label === key ? name : label };
        });

    for (const field of this.filterFields) {
      if (this.options[field.key]) continue;
      switch (field.key) {
        case 'cities':
          this.lookups.allCities().subscribe(res =>
            this.options = { ...this.options, cities: (res.items || []).map(c => ({ value: c.cityId, label: c.name })) });
          break;
        case 'customers':
          this.customerClient.getAll(1, 500).subscribe(res =>
            this.options = {
              ...this.options,
              customers: (res.items || []).map(c => ({ value: c.customerId, label: c.fullName, description: c.mobileNumber }))
            });
          break;
        case 'merchants':
          this.lookups.activeMerchants().subscribe(res =>
            this.options = {
              ...this.options,
              merchants: (res || []).map(m => ({ value: m.merchantId, label: m.fullName, description: m.mobileNumber }))
            });
          break;
        case 'deliveries':
          this.lookups.activeDeliveries().subscribe(res =>
            this.options = {
              ...this.options,
              deliveries: (res || []).map(d => ({ value: d.deliveryId, label: d.fullName, description: d.mobileNumber }))
            });
          break;
        case 'vehicles':
          this.vehicleClient.getMyVehicles(1, 500).subscribe(res =>
            this.options = {
              ...this.options,
              vehicles: (res.items || []).map(v => ({ value: v.vehicleId, label: v.name, description: v.vehicleCode }))
            });
          break;
        case 'orderStates':
          this.options.orderStates = enumOptions('reports.value.', OrderState);
          break;
        case 'paymentMethods':
          this.options.paymentMethods = enumOptions('reports.value.', PaymentMethod);
          break;
        case 'paymentStates':
          this.options.paymentStates = enumOptions('reports.value.', PaymentState);
          break;
        case 'refundStates':
          this.options.refundStates = enumOptions('reports.value.Refund', RefundState);
          break;
        case 'directions':
          this.options.directions = enumOptions('reports.value.', SettlementDirection);
          break;
        case 'partyTypes':
          this.options.partyTypes = enumOptions('reports.value.', LedgerPartyType)
            .filter(o => o.value !== LedgerPartyType.Company);
          break;
      }
    }
  }
}
