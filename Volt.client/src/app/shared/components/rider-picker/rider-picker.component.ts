import {
  Component,
  ElementRef,
  EventEmitter,
  HostListener,
  Input,
  Output,
  inject
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '../../pipes/translate.pipe';
import { LocaleService } from '../../../core/services/locale.service';
import {
  RiderAvailabilityStatus,
  RiderCandidate
} from '../../../core/services/rider-dispatch.service';

/** Matches `.rp__panel` max-height (24rem). */
const PANEL_HEIGHT_PX = 384;
/** Below this the panel opens anyway and the dialog body scrolls. */
const PANEL_MIN_HEIGHT_PX = 180;
/** `.ms__panel` sits 0.4rem from the trigger, plus a little air. */
const PANEL_GAP_PX = 12;

/** Map a rider availability to its dot tone. */
export function riderStatusTone(status: RiderAvailabilityStatus | null | undefined): 'ok' | 'warn' | 'off' {
  switch (status) {
    case RiderAvailabilityStatus.Available:
      return 'ok';
    case RiderAvailabilityStatus.InShiftOffline:
      return 'warn';
    default:
      return 'off';
  }
}

export function riderStatusKey(status: RiderAvailabilityStatus | null | undefined): string {
  switch (status) {
    case RiderAvailabilityStatus.Available:
      return 'riders.statusAvailable';
    case RiderAvailabilityStatus.InShiftOffline:
      return 'riders.statusInShiftOffline';
    default:
      return 'riders.statusOffShift';
  }
}

/** Plain amount, up to 2 decimals, Latin digits (same as the rest of the admin). */
export function formatCashAmount(value: number): string {
  return Number(value || 0).toLocaleString('en-US', { maximumFractionDigits: 2 });
}

/** "Debt 350 / 500 EGP", or "Debt 350 EGP" without a limit. */
export function cashDebtLabel(
  debt: number,
  limit: number | null | undefined,
  translate: (key: string, params?: Record<string, string | number>) => string
): string {
  const currency = translate('riders.currencyShort');
  return limit != null
    ? translate('riders.cashDebtOfLimit', { debt: formatCashAmount(debt), limit: formatCashAmount(limit), currency })
    : translate('riders.cashDebtNoLimit', { debt: formatCashAmount(debt), currency });
}

/**
 * Single rider dropdown for the assign dialog. Same look as `app-multi-select`, plus each rider's
 * availability dot, current shift and open trips. Candidates are expected best-first.
 */
@Component({
  selector: 'app-rider-picker',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslatePipe],
  templateUrl: './rider-picker.component.html',
  styleUrls: ['../multi-select/multi-select.component.css', './rider-picker.component.css'],
  host: {
    '[class.ms-host--open]': 'open'
  }
})
export class RiderPickerComponent {
  private readonly host = inject(ElementRef<HTMLElement>);
  private readonly localeService = inject(LocaleService);

  @Input() candidates: RiderCandidate[] = [];
  @Input() value: number | null = null;
  /** Rider holding the leg now; tagged "Current" in the list. */
  @Input() currentId: number | null = null;
  /** Shown when the selected rider is not among the candidates (e.g. deactivated). */
  @Input() fallbackLabel = '';
  @Input() placeholder = '';
  @Input() disabled = false;
  /**
   * Riders at/over their cash debt limit cannot be picked (delivery trip of a cash order, which the
   * backend refuses). The current rider stays pickable: keeping him is not a new assignment.
   */
  @Input() blockOverDebt = false;
  @Output() valueChange = new EventEmitter<number>();

  open = false;
  /** Panel opens above the trigger (set each time it opens). */
  openUp = false;
  panelMaxHeight = PANEL_HEIGHT_PX;
  query = '';

  get selected(): RiderCandidate | undefined {
    return this.value == null ? undefined : this.candidates.find(c => c.deliveryId === this.value);
  }

  get filtered(): RiderCandidate[] {
    const q = this.query.trim().toLowerCase();
    if (!q) return this.candidates;
    return this.candidates.filter(c =>
      (c.fullName || '').toLowerCase().includes(q)
      || (c.mobileNumber || '').toLowerCase().includes(q)
      || (c.zoneName || '').toLowerCase().includes(q)
      || (c.currentShiftName || '').toLowerCase().includes(q)
    );
  }

  tone(status: RiderAvailabilityStatus): 'ok' | 'warn' | 'off' {
    return riderStatusTone(status);
  }

  statusLabel(status: RiderAvailabilityStatus): string {
    return this.localeService.translate(riderStatusKey(status));
  }

  openTripsLabel(count: number): string {
    const key = count === 0 ? 'riders.openTripsNone' : count === 1 ? 'riders.openTripsOne' : 'riders.openTrips';
    return this.localeService.translate(key, { n: count });
  }

  /** Nothing to say for a rider with no debt and no limit. */
  hasDebtInfo(c: RiderCandidate): boolean {
    return c.cashDebt !== 0 || c.cashDebtLimit != null;
  }

  debtLabel(c: RiderCandidate): string {
    return cashDebtLabel(c.cashDebt, c.cashDebtLimit, (key, params) => this.localeService.translate(key, params));
  }

  isBlocked(c: RiderCandidate): boolean {
    return this.blockOverDebt && c.isOverCashDebtLimit && c.deliveryId !== this.currentId;
  }

  toggleOpen(event?: Event): void {
    event?.stopPropagation();
    if (this.disabled) return;
    this.open = !this.open;
    if (this.open) {
      this.placePanel();
    } else {
      this.query = '';
    }
  }

  /**
   * The picker usually sits in a scrolling dialog body. Open on the side with more room when the
   * panel does not fit below, and cap its height to that room so it is never clipped.
   */
  private placePanel(): void {
    const el = this.host.nativeElement as HTMLElement;
    const rect = el.getBoundingClientRect();
    let top = 0;
    let bottom = window.innerHeight;
    for (let p = el.parentElement; p; p = p.parentElement) {
      const overflowY = getComputedStyle(p).overflowY;
      if (overflowY === 'auto' || overflowY === 'scroll' || overflowY === 'hidden') {
        const box = p.getBoundingClientRect();
        top = Math.max(top, box.top);
        bottom = Math.min(bottom, box.bottom);
        break;
      }
    }
    const below = bottom - rect.bottom - PANEL_GAP_PX;
    const above = rect.top - top - PANEL_GAP_PX;
    this.openUp = below < PANEL_HEIGHT_PX && above > below;
    const room = this.openUp ? above : below;
    this.panelMaxHeight = Math.max(PANEL_MIN_HEIGHT_PX, Math.min(PANEL_HEIGHT_PX, Math.floor(room)));
  }

  pick(candidate: RiderCandidate, event?: Event): void {
    event?.stopPropagation();
    if (this.disabled || this.isBlocked(candidate)) return;
    this.value = candidate.deliveryId;
    this.valueChange.emit(candidate.deliveryId);
    this.open = false;
    this.query = '';
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (this.open && !this.host.nativeElement.contains(event.target as Node)) {
      this.open = false;
      this.query = '';
    }
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.open = false;
    this.query = '';
  }
}
