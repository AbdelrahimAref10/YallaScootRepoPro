import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { CityDto, PagedResultOfCityDto } from '../../../core/services/clientAPI';
import { LocaleService } from '../../../core/services/locale.service';
import {
  RiderCandidate,
  RiderDispatchService,
  SHIFT_ALL_DAYS,
  SaveShiftPayload,
  Shift
} from '../../../core/services/rider-dispatch.service';
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';
import {
  MultiSelectComponent,
  MultiSelectOption
} from '../../../shared/components/multi-select/multi-select.component';
import { memo } from '../../../shared/utils/memo';
import { LookupService } from '../../../core/services/lookup.service';

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

/** Sun–Thu, the Egyptian working week. */
const WORK_DAYS = 1 | 2 | 4 | 8 | 16;
/** Fri + Sat. */
const WEEKEND_DAYS = 32 | 64;

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
  zoneName: string;
  isOnline: boolean;
  currentShiftName: string | null;
}

/** One coloured segment of the 24h timeline, as percentages of the day. */
interface TimelineSegment {
  left: number;
  width: number;
}

@Component({
  selector: 'app-shift-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, TranslatePipe, MultiSelectComponent],
  templateUrl: './shift-form.component.html',
  styleUrls: ['./shift-form.component.css']
})
export class ShiftFormComponent implements OnInit {
  private readonly lookups = inject(LookupService);
  private readonly cityOptionsMemo = memo<MultiSelectOption[]>();

  private readonly localeService = inject(LocaleService);
  private readonly dispatchService = inject(RiderDispatchService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly days = DAYS;
  readonly hourTicks = [0, 3, 6, 9, 12, 15, 18, 21, 24];

  shiftId: number | null = null;
  editingShift: Shift | null = null;
  form: ShiftForm = this.emptyForm();
  cities: CityDto[] = [];
  riderCandidates: RiderCandidate[] = [];
  riderSearch = '';
  isLoading = false;
  isLoadingRiders = false;
  isSaving = false;
  formError = '';
  loadError = '';

  get isEditMode(): boolean {
    return this.shiftId != null;
  }

  get cityOptions(): MultiSelectOption[] {
    return this.cityOptionsMemo([this.cities], () =>
      this.cities.map(c => ({ value: c.cityId, label: c.name })));
  }

  get cityName(): string {
    return this.cities.find(c => c.cityId === this.form.cityId)?.name ?? '';
  }

  /** City riders: candidates first, plus current riders no longer listed (e.g. deactivated). */
  get riderRows(): RiderRow[] {
    const rows: RiderRow[] = this.riderCandidates.map(c => ({
      deliveryId: Number(c.deliveryId),
      fullName: c.fullName,
      mobileNumber: c.mobileNumber,
      zoneName: c.zoneName,
      isOnline: c.isOnline,
      currentShiftName: c.currentShiftName
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
            mobileNumber: r.mobileNumber,
            zoneName: '',
            isOnline: r.isOnline,
            currentShiftName: null
          });
        }
      }
    }
    return rows;
  }

  get filteredRiders(): RiderRow[] {
    const q = this.riderSearch.trim().toLowerCase();
    if (!q) return this.riderRows;
    return this.riderRows.filter(r =>
      r.fullName.toLowerCase().includes(q)
      || (r.mobileNumber || '').includes(q)
      || (r.zoneName || '').toLowerCase().includes(q));
  }

  get selectedRiders(): RiderRow[] {
    const selected = new Set(this.form.deliveryIds);
    return this.riderRows.filter(r => selected.has(r.deliveryId));
  }

  get allFilteredSelected(): boolean {
    const rows = this.filteredRiders;
    return rows.length > 0 && rows.every(r => this.form.deliveryIds.includes(r.deliveryId));
  }

  get timesValid(): boolean {
    return TIME_PATTERN.test(this.form.startTime) && TIME_PATTERN.test(this.form.endTime);
  }

  get crossesMidnight(): boolean {
    return this.timesValid && this.form.endTime <= this.form.startTime;
  }

  /** Shift length, e.g. "8h 30m". */
  get durationLabel(): string {
    if (!this.timesValid) return '—';
    const minutes = this.durationMinutes();
    const h = Math.floor(minutes / 60);
    const m = minutes % 60;
    return m ? `${h}h ${m}m` : `${h}h`;
  }

  get selectedDaysCount(): number {
    return DAYS.filter(d => this.isDayOn(d.bit)).length;
  }

  /** The shift window drawn on a 24h bar; a shift crossing midnight is two segments. */
  get timelineSegments(): TimelineSegment[] {
    if (!this.timesValid) return [];
    const start = this.toMinutes(this.form.startTime);
    const end = this.toMinutes(this.form.endTime);
    const pct = (m: number) => (m / 1440) * 100;
    if (end > start) return [{ left: pct(start), width: pct(end - start) }];
    const segments: TimelineSegment[] = [{ left: pct(start), width: 100 - pct(start) }];
    if (end > 0) segments.push({ left: 0, width: pct(end) });
    return segments;
  }

  ngOnInit(): void {
    this.lookups.activeCities().subscribe({
      next: (result: PagedResultOfCityDto) => {
        this.cities = result.items || [];
      },
      error: () => {
        this.cities = [];
      }
    });

    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.shiftId = Number(idParam);
      this.loadShift(this.shiftId);
    } else {
      const cityParam = this.route.snapshot.queryParamMap.get('cityId');
      if (cityParam) {
        this.form.cityId = Number(cityParam);
        this.loadRiders();
      }
    }
  }

  // ── Form ──────────────────────────────────────────────────────────
  onCityChange(value: unknown): void {
    const cityId = value != null && value !== '' ? Number(value) : null;
    if (cityId === this.form.cityId) return;
    this.form.cityId = cityId;
    // Riders belong to one city; a city change starts the rider list over.
    this.form.deliveryIds = [];
    this.riderSearch = '';
    this.loadRiders();
  }

  isDayOn(bit: number): boolean {
    return (this.form.daysOfWeekMask & bit) === bit;
  }

  toggleDay(bit: number): void {
    this.form.daysOfWeekMask ^= bit;
  }

  isPreset(mask: number): boolean {
    return (this.form.daysOfWeekMask & SHIFT_ALL_DAYS) === mask;
  }

  applyPreset(preset: 'all' | 'work' | 'weekend' | 'none'): void {
    this.form.daysOfWeekMask = {
      all: SHIFT_ALL_DAYS,
      work: WORK_DAYS,
      weekend: WEEKEND_DAYS,
      none: 0
    }[preset];
  }

  readonly presets = { all: SHIFT_ALL_DAYS, work: WORK_DAYS, weekend: WEEKEND_DAYS };

  isRiderSelected(id: number): boolean {
    return this.form.deliveryIds.includes(id);
  }

  toggleRider(id: number): void {
    this.form.deliveryIds = this.isRiderSelected(id)
      ? this.form.deliveryIds.filter(x => x !== id)
      : [...this.form.deliveryIds, id];
  }

  toggleAllFiltered(): void {
    const ids = this.filteredRiders.map(r => r.deliveryId);
    if (this.allFilteredSelected) {
      const drop = new Set(ids);
      this.form.deliveryIds = this.form.deliveryIds.filter(id => !drop.has(id));
    } else {
      this.form.deliveryIds = Array.from(new Set([...this.form.deliveryIds, ...ids]));
    }
  }

  initials(name: string): string {
    return (name || '?')
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map(p => p[0])
      .join('')
      .toUpperCase();
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
    if (!this.timesValid) {
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
    const request$ = this.shiftId != null
      ? this.dispatchService.updateShift(this.shiftId, payload)
      : this.dispatchService.createShift(payload);

    request$.subscribe({
      next: () => {
        this.isSaving = false;
        this.router.navigate(['/main/shifts'], {
          state: { flash: this.isEditMode ? 'shifts.updateSuccess' : 'shifts.createSuccess' }
        });
      },
      error: (error: any) => {
        this.isSaving = false;
        this.formError = this.errorText(error, 'shifts.saveFailed');
      }
    });
  }

  onCancel(): void {
    this.router.navigate(['/main/shifts']);
  }

  // ── Helpers ───────────────────────────────────────────────────────
  private loadShift(id: number): void {
    this.isLoading = true;
    this.dispatchService.getShift(id).subscribe({
      next: (shift) => {
        this.editingShift = shift;
        this.form = {
          cityId: shift.cityId,
          name: shift.name,
          startTime: shift.startTime,
          endTime: shift.endTime,
          daysOfWeekMask: shift.daysOfWeekMask,
          isActive: shift.isActive,
          deliveryIds: shift.riders.map(r => Number(r.deliveryId)).filter(x => Number.isFinite(x))
        };
        this.isLoading = false;
        this.loadRiders();
      },
      error: (error: any) => {
        this.isLoading = false;
        this.loadError = this.errorText(error, 'shifts.loadFailed');
      }
    });
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

  private durationMinutes(): number {
    const start = this.toMinutes(this.form.startTime);
    const end = this.toMinutes(this.form.endTime);
    return end > start ? end - start : 1440 - start + end;
  }

  private toMinutes(hhmm: string): number {
    const [h, m] = hhmm.split(':').map(Number);
    return h * 60 + m;
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

  private errorText(error: any, fallbackKey: string): string {
    return error?.errorMessage || error?.error?.errorMessage || this.localeService.translate(fallbackKey);
  }
}
