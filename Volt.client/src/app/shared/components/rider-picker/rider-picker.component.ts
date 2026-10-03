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
  @Output() valueChange = new EventEmitter<number>();

  open = false;
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

  toggleOpen(event?: Event): void {
    event?.stopPropagation();
    if (this.disabled) return;
    this.open = !this.open;
    if (!this.open) this.query = '';
  }

  pick(candidate: RiderCandidate, event?: Event): void {
    event?.stopPropagation();
    if (this.disabled) return;
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
