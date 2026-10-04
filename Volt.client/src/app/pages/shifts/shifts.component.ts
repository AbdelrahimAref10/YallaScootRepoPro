import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CityClient, CityDto, PagedResultOfCityDto } from '../../core/services/clientAPI';
import { LocaleService } from '../../core/services/locale.service';
import {
  RiderCandidate,
  RiderDispatchService,
  SHIFT_ALL_DAYS,
  SaveShiftPayload,
  Shift
} from '../../core/services/rider-dispatch.service';
import { TranslatePipe } from '../../shared/pipes/translate.pipe';
import { ConfirmDialogComponent } from '../../shared/components/confirm-dialog/confirm-dialog.component';
import {
  MultiSelectComponent,
  MultiSelectOption
} from '../../shared/components/multi-select/multi-select.component';

interface DayChip {
  bit: number;
  key: string;
}

/** Sunday = 1, Monday = 2, … Saturday = 64 (same order as .NET DayOfWeek). */
const DAYS: DayChip[] = [
  { bit: 1, key: 'shifts.daySun' },
  { bit: 2, key: 'shifts.dayMon' },
  { bit: 4, key: 'shifts.dayTue' },
  { bit: 8, key: 'shifts.dayWed' },
  { bit: 16, key: 'shifts.dayThu' },
  { bit: 32, key: 'shifts.dayFri' },
  { bit: 64, key: 'shifts.daySat' }
];

const TIME_PATTERN = /^([01]\d|2[0-3]):[0-5]\d$/;

interface ShiftForm {
  cityId: number | null;
  name: string;
  startTime: string;
  endTime: string;
  daysOfWeekMask: number;
  isActive: boolean;
  deliveryIds: number[];
}

interface RiderRow {
  deliveryId: number;
  fullName: string;
  mobileNumber: string;
}

@Component({
  selector: 'app-shifts',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslatePipe, MultiSelectComponent, ConfirmDialogComponent],
  templateUrl: './shifts.component.html',
  styleUrls: ['./shifts.component.css', '../../shared/styles/entity-form.css']
})
export class ShiftsComponent implements OnInit {
  private readonly localeService = inject(LocaleService);
  private readonly cityClient = inject(CityClient);
  private readonly dispatchService = inject(RiderDispatchService);

  readonly days = DAYS;

  shifts: Shift[] = [];
  cities: CityDto[] = [];
  cityFilter: number | null = null;
  isLoading = false;
  errorMessage = '';
  successMessage = '';

  // Create / edit modal
  showModal = false;
  editingShift: Shift | null = null;
  form: ShiftForm = this.emptyForm();
  formError = '';
  isSaving = false;
  riderCandidates: RiderCandidate[] = [];
  isLoadingRiders = false;

  // Delete
  showConfirmDialog = false;
  pendingDelete: Shift | null = null;
  isDeleting = false;

  get cityOptions(): MultiSelectOption[] {
    return this.cities.map(c => ({ value: c.cityId, label: c.name }));
  }

  /** City riders for the modal: candidates first, plus current riders no longer listed (e.g. deactivated). */
  get riderRows(): RiderRow[] {
    const rows: RiderRow[] = this.riderCandidates.map(c => ({
      deliveryId: Number(c.deliveryId),
      fullName: c.fullName,
      mobileNumber: c.mobileNumber
    }));
    const known = new Set(rows.map(r => r.deliveryId));
    if (this.editingShift && this.editingShift.cityId === this.form.cityId) {
      for (const r of this.editingShift.riders) {
        const deliveryId = Number(r.deliveryId);
        if (Number.isFinite(deliveryId) && !known.has(deliveryId)) {
          known.add(deliveryId);
          rows.push({
            deliveryId,
            fullName: r.fullName,
            mobileNumber: r.mobileNumber
          });
        }
      }
    }
    return rows;
  }

  get riderOptions(): MultiSelectOption[] {
    return this.riderRows.map(r => ({
      value: r.deliveryId,
      label: r.fullName,
      description: r.mobileNumber || '—'
    }));
  }

  get formCrossesMidnight(): boolean {
    return TIME_PATTERN.test(this.form.startTime)
      && TIME_PATTERN.test(this.form.endTime)
      && this.form.endTime <= this.form.startTime;
  }

  ngOnInit(): void {
    this.cityClient.getAll(1, 1000, undefined, true).subscribe({
      next: (result: PagedResultOfCityDto) => {
        this.cities = result.items || [];
      },
      error: () => {
        this.cities = [];
      }
    });
    this.loadShifts();
  }

  loadShifts(): void {
    this.isLoading = true;
    this.errorMessage = '';
    this.dispatchService.getShifts(this.cityFilter).subscribe({
      next: (list) => {
        this.shifts = list;
        this.isLoading = false;
      },
      error: (error: any) => {
        this.isLoading = false;
        this.errorMessage = this.errorText(error, 'shifts.loadFailed');
      }
    });
  }

  onCityFilterChange(value: unknown): void {
    this.cityFilter = value != null && value !== '' ? Number(value) : null;
    this.loadShifts();
  }

  // ── Labels ────────────────────────────────────────────────────────
  crossesMidnight(shift: Shift): boolean {
    return shift.endTime <= shift.startTime;
  }

  isDayOn(mask: number, bit: number): boolean {
    return (mask & bit) === bit;
  }

  isEveryDay(mask: number): boolean {
    return (mask & SHIFT_ALL_DAYS) === SHIFT_ALL_DAYS;
  }

  onlineCount(shift: Shift): number {
    return shift.riders.filter(r => r.isOnline).length;
  }

  // ── Create / edit ─────────────────────────────────────────────────
  onAdd(): void {
    this.editingShift = null;
    this.form = this.emptyForm();
    this.form.cityId = this.cityFilter;
    this.openModal();
  }

  onEdit(shift: Shift): void {
    this.editingShift = shift;
    this.form = {
      cityId: shift.cityId,
      name: shift.name,
      startTime: shift.startTime,
      endTime: shift.endTime,
      daysOfWeekMask: shift.daysOfWeekMask,
      isActive: shift.isActive,
      deliveryIds: shift.riders.map(r => Number(r.deliveryId)).filter(id => Number.isFinite(id))
    };
    this.openModal();
  }

  onFormCityChange(value: unknown): void {
    const cityId = value != null && value !== '' ? Number(value) : null;
    if (cityId === this.form.cityId) return;
    this.form.cityId = cityId;
    // Riders belong to one city; a city change starts the rider list over.
    this.form.deliveryIds = [];
    this.loadRiders();
  }

  toggleDay(bit: number): void {
    this.form.daysOfWeekMask ^= bit;
  }

  setEveryDay(): void {
    this.form.daysOfWeekMask = this.isEveryDay(this.form.daysOfWeekMask) ? 0 : SHIFT_ALL_DAYS;
  }

  onRidersChange(values: Array<string | number | boolean>): void {
    this.form.deliveryIds = (values || [])
      .map(v => Number(v))
      .filter(id => Number.isFinite(id) && id > 0);
  }

  onSave(): void {
    this.formError = '';
    const name = this.form.name.trim();
    if (!this.form.cityId) {
      this.formError = this.localeService.translate('shifts.cityRequired');
      return;
    }
    if (!name) {
      this.formError = this.localeService.translate('shifts.nameRequired');
      return;
    }
    if (!TIME_PATTERN.test(this.form.startTime) || !TIME_PATTERN.test(this.form.endTime)) {
      this.formError = this.localeService.translate('shifts.timeRequired');
      return;
    }
    if (!(this.form.daysOfWeekMask & SHIFT_ALL_DAYS)) {
      this.formError = this.localeService.translate('shifts.daysRequired');
      return;
    }

    const payload: SaveShiftPayload = {
      cityId: this.form.cityId,
      name,
      startTime: this.form.startTime,
      endTime: this.form.endTime,
      daysOfWeekMask: this.form.daysOfWeekMask & SHIFT_ALL_DAYS,
      isActive: this.form.isActive,
      deliveryIds: [...this.form.deliveryIds]
    };

    this.isSaving = true;
    const request$ = this.editingShift
      ? this.dispatchService.updateShift(this.editingShift.shiftId, payload)
      : this.dispatchService.createShift(payload);
    const successKey = this.editingShift ? 'shifts.updateSuccess' : 'shifts.createSuccess';

    request$.subscribe({
      next: () => {
        this.isSaving = false;
        this.closeModal();
        this.showSuccess(this.localeService.translate(successKey));
        this.loadShifts();
      },
      error: (error: any) => {
        this.isSaving = false;
        this.formError = this.errorText(error, 'shifts.saveFailed');
      }
    });
  }

  closeModal(): void {
    this.showModal = false;
    this.editingShift = null;
    this.formError = '';
    this.riderCandidates = [];
  }

  // ── Delete ────────────────────────────────────────────────────────
  onDelete(shift: Shift): void {
    if (this.isDeleting) return;
    this.pendingDelete = shift;
    this.showConfirmDialog = true;
  }

  onCancelDelete(): void {
    this.showConfirmDialog = false;
    this.pendingDelete = null;
  }

  onConfirmDelete(): void {
    if (!this.pendingDelete || this.isDeleting) return;
    this.isDeleting = true;
    this.dispatchService.deleteShift(this.pendingDelete.shiftId).subscribe({
      next: () => {
        this.isDeleting = false;
        this.onCancelDelete();
        this.showSuccess(this.localeService.translate('shifts.deleteSuccess'));
        this.loadShifts();
      },
      error: (error: any) => {
        this.isDeleting = false;
        this.onCancelDelete();
        this.errorMessage = this.errorText(error, 'shifts.deleteFailed');
      }
    });
  }

  // ── Helpers ───────────────────────────────────────────────────────
  private openModal(): void {
    this.formError = '';
    this.showModal = true;
    this.loadRiders();
  }

  private loadRiders(): void {
    this.riderCandidates = [];
    const cityId = this.form.cityId;
    if (!cityId) return;
    this.isLoadingRiders = true;
    this.dispatchService.getCandidatesForCity(cityId).subscribe({
      next: (list) => {
        if (this.form.cityId === cityId) {
          this.riderCandidates = list;
        }
        this.isLoadingRiders = false;
      },
      error: (error: any) => {
        this.isLoadingRiders = false;
        this.formError = this.errorText(error, 'orders.deliveriesLoadFailed');
      }
    });
  }

  private emptyForm(): ShiftForm {
    return {
      cityId: null,
      name: '',
      startTime: '08:00',
      endTime: '16:00',
      daysOfWeekMask: SHIFT_ALL_DAYS,
      isActive: true,
      deliveryIds: []
    };
  }

  private showSuccess(message: string): void {
    this.successMessage = message;
    this.errorMessage = '';
    setTimeout(() => {
      this.successMessage = '';
    }, 5000);
  }

  private errorText(error: any, fallbackKey: string): string {
    return error?.errorMessage || error?.error?.errorMessage || this.localeService.translate(fallbackKey);
  }
}
