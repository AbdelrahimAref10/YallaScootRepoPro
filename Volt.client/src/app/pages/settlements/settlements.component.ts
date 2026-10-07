import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  AdminSettlementClient,
  CreateSettlementVoucherCommand,
  DeliveryClient,
  DeliveryLookupDto,
  JournalDirection,
  LedgerPartyType,
  MerchantClient,
  MerchantLookupDto,
  OrderJournalEntryKind,
  PartyLedgerDto,
  SettlementAllocationKind,
  SettlementDirection,
  SettlementOpenItemDto,
  SettlementSummaryDto,
  SettlementVoucherDetailDto,
  SettlementVoucherDto
} from '../../core/services/clientAPI';
import { LocaleService } from '../../core/services/locale.service';
import { TranslatePipe } from '../../shared/pipes/translate.pipe';
import {
  MultiSelectComponent,
  MultiSelectOption
} from '../../shared/components/multi-select/multi-select.component';
import { HasPermissionDirective } from '../../shared/directives/has-permission.directive';

type SettlementTab = 'delivery' | 'merchant';

/** One line of the "what this voucher settles" preview. */
interface PreviewLine {
  item: SettlementOpenItemDto;
  amount: number;
  full: boolean;
}

/**
 * Settlement vouchers: collect from / pay a delivery (cash and commission are netted) or pay a merchant,
 * fully or partially. The amount is applied to the oldest open orders first.
 */
@Component({
  selector: 'app-settlements',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslatePipe, MultiSelectComponent, HasPermissionDirective],
  templateUrl: './settlements.component.html',
  styleUrls: ['./settlements.component.css']
})
export class SettlementsComponent implements OnInit {
  private readonly localeService = inject(LocaleService);
  private readonly settlementClient = inject(AdminSettlementClient);
  private readonly deliveryClient = inject(DeliveryClient);
  private readonly merchantClient = inject(MerchantClient);

  readonly JournalDirection = JournalDirection;
  readonly SettlementDirection = SettlementDirection;

  activeTab: SettlementTab = 'delivery';
  deliveries: DeliveryLookupDto[] = [];
  merchants: MerchantLookupDto[] = [];
  isLoadingLookups = false;

  partyId: number | null = null;
  summary: SettlementSummaryDto | null = null;
  ledger: PartyLedgerDto | null = null;
  vouchers: SettlementVoucherDto[] = [];
  openVoucher: SettlementVoucherDetailDto | null = null;
  isLoadingParty = false;

  amount: number | null = null;
  note = '';
  /** Generated once per form so a double click posts one voucher. */
  private requestId = crypto.randomUUID();
  isSubmitting = false;
  errorMessage = '';
  successMessage = '';

  get partyType(): LedgerPartyType {
    return this.activeTab === 'merchant' ? LedgerPartyType.Merchant : LedgerPartyType.Delivery;
  }

  get partyOptions(): MultiSelectOption[] {
    return this.activeTab === 'merchant'
      ? this.merchants.filter(m => m.merchantId != null).map(m => ({
          value: m.merchantId as number,
          label: m.fullName || String(m.merchantId),
          description: m.mobileNumber || '—'
        }))
      : this.deliveries.filter(d => d.deliveryId != null).map(d => ({
          value: d.deliveryId as number,
          label: d.fullName || String(d.deliveryId),
          description: d.mobileNumber || '—'
        }));
  }

  get isCollecting(): boolean {
    return this.summary?.direction === SettlementDirection.CollectFromParty;
  }

  /** Delivery cash and commission that cancel out can be settled without cash. */
  get allowsZero(): boolean {
    return !!this.summary && this.summary.cashOwedToCompany > 0 && this.summary.owedByCompany > 0;
  }

  get amountError(): string | null {
    if (!this.summary?.direction) return null;
    const value = this.amount ?? 0;
    if (value < 0 || (value === 0 && !this.allowsZero)) return this.localeService.translate('settlements.amountRequired');
    if (value > this.summary.maxAmount) {
      return this.localeService.translate('settlements.amountTooHigh', { max: this.summary.maxAmount.toFixed(2) });
    }
    return null;
  }

  /** Same allocation the server does: the smaller delivery side is offset, the rest goes oldest first. */
  /** preview builds new lines on every check; keep the rows by their open item. */
  trackPreviewLine(_: number, line: PreviewLine): SettlementOpenItemDto {
    return line.item;
  }

  get preview(): PreviewLine[] {
    if (!this.summary?.direction || this.amountError) return [];
    const collecting = this.isCollecting;
    const isDelivery = this.partyType === LedgerPartyType.Delivery;
    const smallSide = isDelivery ? (collecting ? this.summary.owedByCompany : this.summary.cashOwedToCompany) : 0;
    let remaining = smallSide + (this.amount ?? 0);

    const lines: PreviewLine[] = [];
    for (const item of this.summary.openItems) {
      const isCash = item.kind === SettlementAllocationKind.DeliveryCash || item.kind === SettlementAllocationKind.DeliveryLegacyFloat;
      const onLargeSide = !isDelivery || isCash === collecting;
      const take = onLargeSide ? Math.min(item.open, remaining) : item.open;
      if (onLargeSide) remaining -= take;
      if (take > 0) lines.push({ item, amount: take, full: take >= item.open });
    }
    return lines;
  }

  ngOnInit(): void {
    this.loadLookups();
  }

  setTab(tab: SettlementTab): void {
    if (this.activeTab === tab) return;
    this.activeTab = tab;
    this.partyId = null;
    this.resetParty();
  }

  onPartySelected(value: unknown): void {
    this.partyId = value != null && value !== '' ? Number(value) : null;
    this.resetParty();
    if (this.partyId) this.loadParty();
  }

  private resetParty(): void {
    this.summary = null;
    this.ledger = null;
    this.vouchers = [];
    this.openVoucher = null;
    this.amount = null;
    this.note = '';
    this.errorMessage = '';
  }

  private loadLookups(): void {
    this.isLoadingLookups = true;
    let pending = 2;
    const done = () => {
      pending -= 1;
      if (pending <= 0) this.isLoadingLookups = false;
    };
    this.deliveryClient.getActive().subscribe({ next: list => { this.deliveries = list || []; done(); }, error: () => done() });
    this.merchantClient.getActive().subscribe({ next: list => { this.merchants = list || []; done(); }, error: () => done() });
  }

  loadParty(): void {
    if (!this.partyId) return;
    const partyType = this.partyType;
    const partyId = this.partyId;
    this.isLoadingParty = true;

    this.settlementClient.getSummary(partyType, partyId).subscribe({
      next: summary => {
        this.summary = summary;
        this.amount = summary.maxAmount;
        this.isLoadingParty = false;
      },
      error: (error: any) => {
        this.isLoadingParty = false;
        this.showError(error?.errorMessage || this.localeService.translate('settlements.ledgerFailed'));
      }
    });
    this.settlementClient.getLedger(partyType, partyId).subscribe({ next: ledger => (this.ledger = ledger) });
    this.settlementClient.getVouchers(partyType, partyId, 1, 20).subscribe({ next: page => (this.vouchers = page.items ?? []) });
  }

  onSubmit(): void {
    if (!this.partyId || !this.summary?.direction || this.amountError) return;

    this.isSubmitting = true;
    this.settlementClient.createVoucher(CreateSettlementVoucherCommand.fromJS({
      partyType: this.partyType,
      partyId: this.partyId,
      amount: this.amount ?? 0,
      note: this.note.trim() || null,
      requestId: this.requestId
    })).subscribe({
      next: voucher => {
        this.isSubmitting = false;
        this.requestId = crypto.randomUUID();
        this.showSuccess(this.localeService.translate('settlements.voucherPosted', { no: voucher.voucherNo }));
        this.loadParty();
        this.openVoucher = voucher;
      },
      error: (error: any) => {
        this.isSubmitting = false;
        this.showError(error?.errorMessage || this.localeService.translate('settlements.failed'));
      }
    });
  }

  toggleVoucher(voucher: SettlementVoucherDto): void {
    if (this.openVoucher?.settlementVoucherId === voucher.settlementVoucherId) {
      this.openVoucher = null;
      return;
    }
    this.settlementClient.getVoucher(voucher.settlementVoucherId).subscribe({ next: detail => (this.openVoucher = detail) });
  }

  itemLabel(kind: SettlementAllocationKind): string {
    return this.localeService.translate(`settlements.item.${SettlementAllocationKind[kind]}`);
  }

  directionLabel(direction: SettlementDirection | undefined | null): string {
    return direction === SettlementDirection.CollectFromParty
      ? this.localeService.translate('settlements.collect')
      : this.localeService.translate('settlements.pay');
  }

  getJournalKindLabel(kind: OrderJournalEntryKind): string {
    const key = `orders.journalKind.${OrderJournalEntryKind[kind]}`;
    const translated = this.localeService.translate(key);
    return translated === key ? String(kind) : translated;
  }

  private showSuccess(message: string): void {
    this.successMessage = message;
    this.errorMessage = '';
    setTimeout(() => (this.successMessage = ''), 6000);
  }

  private showError(message: string): void {
    this.errorMessage = message;
    this.successMessage = '';
    setTimeout(() => (this.errorMessage = ''), 6000);
  }
}
