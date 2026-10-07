import {
  AfterViewInit,
  Component,
  DestroyRef,
  ElementRef,
  OnInit,
  ViewChild,
  computed,
  effect,
  inject,
  signal
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { Subscription } from 'rxjs';
import { Chart, registerables, TooltipItem } from 'chart.js';
import {
  AdminHomeClient,
  DashboardAmountDto,
  DashboardBucket,
  DashboardDto,
  DashboardTrendPointDto,
  LedgerPartyType,
  OrderState
} from '../../core/services/clientAPI';
import { LocaleService } from '../../core/services/locale.service';
import { AuthService } from '../../core/services/auth.service';
import { ThemeService } from '../../core/services/theme.service';
import { TranslatePipe } from '../../shared/pipes/translate.pipe';
import { HasPermissionDirective } from '../../shared/directives/has-permission.directive';
import { MultiSelectComponent, MultiSelectOption } from '../../shared/components/multi-select/multi-select.component';
import { LookupService } from '../../core/services/lookup.service';

Chart.register(...registerables);

type PresetKey = 'today' | '7d' | '30d' | 'month' | 'lastMonth' | 'ytd' | 'custom';
type TrendMetric = 'profit' | 'bookings' | 'orders';

interface Preset {
  key: Exclude<PresetKey, 'custom'>;
  labelKey: string;
}

interface BarRow {
  key: string;
  labelKey: string;
  amount: number;
  count?: number;
  share: number;
}

const ISO = (d: Date): string =>
  `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, TranslatePipe, HasPermissionDirective, MultiSelectComponent],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.css']
})
export class DashboardComponent implements OnInit, AfterViewInit {
  private readonly lookups = inject(LookupService);
  @ViewChild('trendCanvas') trendCanvas?: ElementRef<HTMLCanvasElement>;

  private readonly client = inject(AdminHomeClient);
  private readonly locale = inject(LocaleService);
  private readonly theme = inject(ThemeService);
  private readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);

  readonly OrderState = OrderState;
  readonly PartyType = LedgerPartyType;

  readonly presets: Preset[] = [
    { key: 'today', labelKey: 'dash.preset.today' },
    { key: '7d', labelKey: 'dash.preset.7d' },
    { key: '30d', labelKey: 'dash.preset.30d' },
    { key: 'month', labelKey: 'dash.preset.month' },
    { key: 'lastMonth', labelKey: 'dash.preset.lastMonth' },
    { key: 'ytd', labelKey: 'dash.preset.ytd' }
  ];

  readonly userName = this.auth.getUserData()?.userName || '';
  readonly greetingKey = this.resolveGreeting();

  preset = signal<PresetKey>('30d');
  fromDate = '';
  toDate = '';
  cityIds = signal<number[]>([]);
  cityOptions = signal<MultiSelectOption[]>([]);

  data = signal<DashboardDto | null>(null);
  loading = signal(false);
  error = signal('');
  metric = signal<TrendMetric>('profit');

  private chart: Chart | null = null;
  private viewReady = false;
  private request?: Subscription;

  // ---------- Derived view models ----------
  readonly profit = computed(() => this.data()?.profit);
  readonly position = computed(() => this.data()?.position);
  readonly orders = computed(() => this.data()?.orders);

  readonly incomeRows = computed<BarRow[]>(() => this.toRows(this.profit()?.income ?? [], 'dash.income.'));
  readonly costRows = computed<BarRow[]>(() => this.toRows(this.profit()?.costs ?? [], 'dash.cost.'));
  readonly grossIncome = computed(() => (this.profit()?.income ?? []).reduce((s, i) => s + i.amount, 0));
  readonly totalCosts = computed(() => (this.profit()?.costs ?? []).reduce((s, i) => s + i.amount, 0));

  readonly owedRows = computed<BarRow[]>(() => this.toRows(this.position()?.owedToCompanyItems ?? [], 'dash.owed.'));
  readonly oweRows = computed<BarRow[]>(() => this.toRows(this.position()?.companyOwesItems ?? [], 'dash.owe.'));

  readonly boards = computed(() => {
    const d = this.data();
    return [
      { key: 'merchants', titleKey: 'dash.topMerchants', subKey: 'dash.topMerchantsSub', list: d?.topMerchants ?? [] },
      { key: 'riders', titleKey: 'dash.topRiders', subKey: 'dash.topRidersSub', list: d?.topDeliveries ?? [] },
      { key: 'cities', titleKey: 'dash.topCities', subKey: 'dash.topCitiesSub', list: d?.topCities ?? [] }
    ];
  });

  readonly pipelineMax = computed(() => Math.max(1, ...(this.orders()?.pipeline ?? []).map(p => p.count)));

  readonly paymentsTotal = computed(() => {
    const p = this.data()?.payments;
    return p ? p.cashPaid + p.payPalPaid : 0;
  });

  readonly cashShare = computed(() => {
    const p = this.data()?.payments;
    const total = this.paymentsTotal();
    return p && total > 0 ? (p.cashPaid / total) * 100 : 0;
  });

  readonly periodLabel = computed(() => {
    const d = this.data();
    if (!d) return '';
    return `${this.formatDate(d.from)} – ${this.formatDate(d.to)}`;
  });

  readonly previousLabel = computed(() => {
    const d = this.data();
    if (!d) return '';
    return `${this.formatDate(d.previousFrom)} – ${this.formatDate(d.previousTo)}`;
  });

  constructor() {
    // Re-draw the chart when the language or theme changes (labels and colors live in the canvas).
    effect(() => {
      this.locale.locale();
      this.theme.theme();
      this.metric();
      const d = this.data();
      if (d) queueMicrotask(() => this.renderChart());
    });
    this.destroyRef.onDestroy(() => {
      this.request?.unsubscribe();
      this.chart?.destroy();
    });
  }

  ngOnInit(): void {
    this.lookups.allCities().subscribe({
      next: res => this.cityOptions.set((res.items || []).map(c => ({ value: c.cityId, label: c.name })))
    });
    this.applyPreset('30d');
  }

  ngAfterViewInit(): void {
    this.viewReady = true;
    this.renderChart();
  }

  // ---------- Filters ----------
  applyPreset(key: Exclude<PresetKey, 'custom'>): void {
    const today = new Date();
    const start = new Date(today);
    switch (key) {
      case 'today': break;
      case '7d': start.setDate(today.getDate() - 6); break;
      case '30d': start.setDate(today.getDate() - 29); break;
      case 'month': start.setDate(1); break;
      case 'lastMonth': {
        const first = new Date(today.getFullYear(), today.getMonth() - 1, 1);
        const last = new Date(today.getFullYear(), today.getMonth(), 0);
        this.setRange(first, last, key);
        return;
      }
      case 'ytd': start.setMonth(0, 1); break;
    }
    this.setRange(start, today, key);
  }

  onDateChange(): void {
    if (!this.fromDate || !this.toDate) return;
    if (this.fromDate > this.toDate) [this.fromDate, this.toDate] = [this.toDate, this.fromDate];
    this.preset.set('custom');
    this.load();
  }

  onCitiesChange(values: Array<string | number | boolean>): void {
    const next = values.map(Number).filter(Number.isFinite);
    const same = next.length === this.cityIds().length && next.every(id => this.cityIds().includes(id));
    if (same) return;
    this.cityIds.set(next);
    this.load();
  }

  refresh(): void {
    this.load();
  }

  private setRange(from: Date, to: Date, key: PresetKey): void {
    this.fromDate = ISO(from);
    this.toDate = ISO(to);
    this.preset.set(key);
    this.load();
  }

  private load(): void {
    this.request?.unsubscribe();
    this.loading.set(true);
    this.error.set('');
    const cities = this.cityIds();
    this.request = this.client
      .getDashboard(new Date(this.fromDate), new Date(this.toDate), cities.length ? cities : null)
      .subscribe({
        next: d => {
          this.data.set(d);
          this.loading.set(false);
        },
        error: err => {
          this.error.set(err?.result?.errorMessage || err?.errorMessage || this.locale.translate('dash.loadFailed'));
          this.loading.set(false);
        }
      });
  }

  // ---------- Formatting ----------
  money(value: number | null | undefined, compact = false): string {
    const n = value ?? 0;
    if (compact && Math.abs(n) >= 10000) {
      return new Intl.NumberFormat(this.numberLocale(), { notation: 'compact', maximumFractionDigits: 1 }).format(n);
    }
    return n.toLocaleString(this.numberLocale(), { minimumFractionDigits: 0, maximumFractionDigits: 2 });
  }

  count(value: number | null | undefined): string {
    return (value ?? 0).toLocaleString(this.numberLocale());
  }

  plural(value: number | null | undefined, oneKey: string, manyKey: string): string {
    const n = value ?? 0;
    return `${this.count(n)} ${this.locale.translate(n === 1 ? oneKey : manyKey)}`;
  }

  percent(value: number | null | undefined): string {
    return `${(value ?? 0).toLocaleString(this.numberLocale(), { maximumFractionDigits: 1 })}%`;
  }

  delta(value: number | null | undefined): string {
    const n = value ?? 0;
    const sign = n > 0 ? '+' : n < 0 ? '−' : '';
    return `${sign}${Math.abs(n).toLocaleString(this.numberLocale(), { maximumFractionDigits: 1 })}%`;
  }

  deltaClass(value: number | null | undefined, inverse = false): string {
    const n = value ?? 0;
    if (n === 0) return 'dsh-delta--flat';
    return (n > 0) !== inverse ? 'dsh-delta--up' : 'dsh-delta--down';
  }

  stateLabel(state: OrderState): string {
    const key = `reports.value.${OrderState[state]}`;
    const t = this.locale.translate(key);
    return t === key ? OrderState[state] : t;
  }

  partyKind(type: LedgerPartyType): string {
    return type === LedgerPartyType.Merchant ? 'dash.party.merchant' : 'dash.party.delivery';
  }

  rankShare(amount: number, list: { amount: number }[]): number {
    const max = Math.max(...list.map(i => i.amount), 0);
    return max > 0 ? Math.max(4, (amount / max) * 100) : 0;
  }

  stateTone(state: OrderState): string {
    switch (state) {
      case OrderState.Completed: return 'dsh-chip--ok';
      case OrderState.Cancelled:
      case OrderState.CustomerRejectedReceipt: return 'dsh-chip--bad';
      case OrderState.Pending:
      case OrderState.MerchantPending: return 'dsh-chip--warn';
      default: return 'dsh-chip--info';
    }
  }

  formatDate(value: Date | string | undefined): string {
    if (!value) return '';
    return new Date(value).toLocaleDateString(this.numberLocale(), { day: 'numeric', month: 'short', year: 'numeric' });
  }

  formatTime(value: Date | string | undefined): string {
    return value ? new Date(value).toLocaleTimeString(this.numberLocale(), { hour: 'numeric', minute: '2-digit' }) : '';
  }

  formatDateTime(value: Date | string | undefined): string {
    return value
      ? new Date(value).toLocaleString(this.numberLocale(), { day: 'numeric', month: 'short', hour: 'numeric', minute: '2-digit' })
      : '';
  }



  private numberLocale(): string {
    return this.locale.locale() === 'ar' ? 'ar-EG' : 'en-US';
  }

  private toRows(items: DashboardAmountDto[], prefix: string): BarRow[] {
    const max = Math.max(...items.map(i => Math.abs(i.amount)), 0);
    return items.map(i => ({
      key: i.key,
      labelKey: prefix + i.key,
      amount: i.amount,
      count: i.count,
      share: max > 0 ? (Math.abs(i.amount) / max) * 100 : 0
    }));
  }

  private resolveGreeting(): string {
    const hour = new Date().getHours();
    if (hour < 12) return 'dash.greeting.morning';
    if (hour < 18) return 'dash.greeting.afternoon';
    return 'dash.greeting.evening';
  }

  // ---------- Trend chart ----------
  private cssVar(name: string, fallback: string): string {
    return getComputedStyle(document.documentElement).getPropertyValue(name).trim() || fallback;
  }

  private bucketLabel(point: DashboardTrendPointDto, bucket: DashboardBucket): string {
    const date = new Date(point.date);
    const loc = this.numberLocale();
    if (bucket === DashboardBucket.Month) return date.toLocaleDateString(loc, { month: 'short', year: '2-digit' });
    return date.toLocaleDateString(loc, { day: 'numeric', month: 'short' });
  }

  private renderChart(): void {
    const canvas = this.trendCanvas?.nativeElement;
    const d = this.data();
    if (!this.viewReady || !canvas || !d) return;

    const metric = this.metric();
    const points = d.trend;
    const values = points.map(p =>
      metric === 'profit' ? p.netProfit : metric === 'bookings' ? p.grossBookings : p.orders);
    const labels = points.map(p => this.bucketLabel(p, d.bucket));

    const accent = this.cssVar('--volt-accent', '#e10600');
    const muted = this.cssVar('--volt-text-muted', '#8d868f');
    const grid = this.cssVar('--volt-border', '#e6e6e6');
    const surface = this.cssVar('--volt-surface', '#ffffff');
    const text = this.cssVar('--volt-text-primary', '#161316');
    const font = getComputedStyle(document.body).fontFamily || 'sans-serif';
    const isMoney = metric !== 'orders';
    const rtl = this.locale.isRtl();

    // Positive bars in the brand color, losses in a muted tone so they read as "below zero", not as another series.
    const colors = values.map(v => (v < 0 ? muted : accent));

    const tooltipTitle = (items: TooltipItem<'bar'>[]) => {
      const p = points[items[0].dataIndex];
      if (d.bucket === DashboardBucket.Day) return this.formatDate(p.date);
      const start = new Date(p.date);
      const end = new Date(start);
      if (d.bucket === DashboardBucket.Week) end.setDate(start.getDate() + 6);
      else end.setMonth(start.getMonth() + 1, 0);
      return `${this.formatDate(start)} – ${this.formatDate(end)}`;
    };

    const config = {
      type: 'bar' as const,
      data: {
        labels,
        datasets: [{
          data: values,
          backgroundColor: colors,
          hoverBackgroundColor: colors.map(c => c + 'cc'),
          borderRadius: { topLeft: 4, topRight: 4, bottomLeft: 0, bottomRight: 0 },
          borderSkipped: 'start' as const,
          maxBarThickness: 28,
          categoryPercentage: 0.8,
          barPercentage: 0.85
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        animation: { duration: 250 },
        interaction: { mode: 'index' as const, intersect: false },
        layout: { padding: { top: 8 } },
        plugins: {
          legend: { display: false },
          tooltip: {
            rtl,
            backgroundColor: surface,
            titleColor: text,
            bodyColor: text,
            borderColor: grid,
            borderWidth: 1,
            padding: 12,
            cornerRadius: 10,
            displayColors: false,
            titleFont: { family: font, weight: 600 as const, size: 12 },
            bodyFont: { family: font, weight: 700 as const, size: 14 },
            footerFont: { family: font, weight: 500 as const, size: 11 },
            footerColor: muted,
            callbacks: {
              title: tooltipTitle,
              label: (item: TooltipItem<'bar'>) => {
                const v = Number(item.raw) || 0;
                return isMoney
                  ? `${this.money(v)} ${this.locale.translate('common.currency')}`
                  : `${this.count(v)} ${this.locale.translate('dash.metric.ordersUnit')}`;
              },
              footer: (items: TooltipItem<'bar'>[]) => {
                const p = points[items[0].dataIndex];
                return [
                  `${this.locale.translate('dash.metric.orders')}: ${this.count(p.orders)} · ${this.locale.translate('dash.completed')}: ${this.count(p.completed)} · ${this.locale.translate('dash.cancelled')}: ${this.count(p.cancelled)}`
                ];
              }
            }
          }
        },
        scales: {
          x: {
            reverse: rtl,
            grid: { display: false },
            border: { color: grid },
            ticks: { color: muted, font: { family: font, size: 11 }, maxRotation: 0, autoSkip: true, autoSkipPadding: 12 }
          },
          y: {
            position: (rtl ? 'right' : 'left') as 'left' | 'right',
            beginAtZero: true,
            grid: { color: grid, drawTicks: false },
            border: { display: false },
            ticks: {
              color: muted,
              font: { family: font, size: 11 },
              padding: 8,
              maxTicksLimit: 5,
              precision: isMoney ? undefined : 0,
              callback: (v: string | number) => this.money(Number(v), true)
            }
          }
        }
      }
    };

    if (this.chart) this.chart.destroy();
    this.chart = new Chart(canvas, config);
  }
}
