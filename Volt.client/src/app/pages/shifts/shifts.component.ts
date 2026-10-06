import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { CityClient, CityDto, PagedResultOfCityDto } from '../../core/services/clientAPI';
import { LocaleService } from '../../core/services/locale.service';
import { RiderDispatchService, SHIFT_ALL_DAYS, Shift } from '../../core/services/rider-dispatch.service';
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

@Component({
  selector: 'app-shifts',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslatePipe, MultiSelectComponent, ConfirmDialogComponent],
  templateUrl: './shifts.component.html',
  styleUrls: ['./shifts.component.css']
})
export class ShiftsComponent implements OnInit {
  private readonly localeService = inject(LocaleService);
  private readonly cityClient = inject(CityClient);
  private readonly dispatchService = inject(RiderDispatchService);
  private readonly router = inject(Router);

  readonly days = DAYS;

  shifts: Shift[] = [];
  cities: CityDto[] = [];
  cityFilter: number | null = null;
  isLoading = false;
  errorMessage = '';
  successMessage = '';

  // Delete
  showConfirmDialog = false;
  pendingDelete: Shift | null = null;
  isDeleting = false;

  get cityOptions(): MultiSelectOption[] {
    return this.cities.map(c => ({ value: c.cityId, label: c.name }));
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
    // Set by the shift form page after a successful save.
    const flash = history.state?.flash as string | undefined;
    if (flash) {
      this.showSuccess(this.localeService.translate(flash));
    }
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

  // ── Create / edit (own page) ──────────────────────────────────────
  onAdd(): void {
    this.router.navigate(['/main/shifts/new'], {
      queryParams: this.cityFilter ? { cityId: this.cityFilter } : {}
    });
  }

  onEdit(shift: Shift): void {
    this.router.navigate(['/main/shifts', shift.shiftId, 'edit']);
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
