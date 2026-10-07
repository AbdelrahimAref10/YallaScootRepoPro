import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { catchError, forkJoin, of } from 'rxjs';
import { AdminDeliveryClient, DeliveryDto } from '../../core/services/clientAPI';
import { LocaleService } from '../../core/services/locale.service';
import {
  RiderCandidate,
  RiderCashDebtInfo,
  RiderDispatchService,
  isOverCashDebtLimit
} from '../../core/services/rider-dispatch.service';
import {
  formatCashAmount,
  riderStatusKey,
  riderStatusTone
} from '../../shared/components/rider-picker/rider-picker.component';
import { TranslatePipe } from '../../shared/pipes/translate.pipe';
import { ConfirmDialogComponent } from '../../shared/components/confirm-dialog/confirm-dialog.component';
import {
  MultiSelectComponent,
  MultiSelectOption
} from '../../shared/components/multi-select/multi-select.component';
import { HasPermissionDirective } from '../../shared/directives/has-permission.directive';
import { memo } from '../../shared/utils/memo';
import { PortalDirective } from '../../shared/directives/portal.directive';

@Component({
  selector: 'app-deliveries',
  standalone: true,
  imports: [PortalDirective, CommonModule, FormsModule, RouterModule, TranslatePipe, MultiSelectComponent, ConfirmDialogComponent, HasPermissionDirective],
  templateUrl: './deliveries.component.html',
  styleUrls: ['./deliveries.component.css', '../../shared/styles/list-filters.css', '../../shared/styles/entity-form.css']
})
export class DeliveriesComponent implements OnInit {
  private readonly deletedFilterOptionsMemo = memo<MultiSelectOption[]>();

  private readonly localeService = inject(LocaleService);
  private readonly deliveryClient = inject(AdminDeliveryClient);
  private readonly router = inject(Router);
  private readonly dispatchService = inject(RiderDispatchService);

  deliveries: DeliveryDto[] = [];
  /** Live shift / online status per rider, from ForAssign by city. */
  availability = new Map<number, RiderCandidate>();
  /** Cash debt and its limit per rider (not in the generated `DeliveryDto`). */
  debts = new Map<number, RiderCashDebtInfo>();
  searchTerm = '';
  deletedFilterValue: 'active' | 'deleted' = 'active';
  isLoading = false;
  isDeleting = false;
  pendingDeleteId: number | null = null;
  showConfirmDialog = false;
  errorMessage = '';
  successMessage = '';

  // Cash debt limit dialog
  limitTarget: DeliveryDto | null = null;
  /** Bound to a number input: number, or null / '' when empty. */
  limitInput: number | string | null = null;
  limitError = '';
  isSavingLimit = false;

  get deletedFilterOptions(): MultiSelectOption[] {
    return this.deletedFilterOptionsMemo([this.localeService.locale()], () =>
      [
        { value: 'active', label: this.localeService.translate('deliveries.filterNotDeleted') },
        { value: 'deleted', label: this.localeService.translate('deliveries.filterDeleted') }
      ]);
  }

  get isDeletedFilter(): boolean | null {
    return this.deletedFilterValue === 'deleted' ? true : null;
  }

  ngOnInit(): void {
    this.loadDeliveries();
  }

  loadDeliveries(): void {
    this.isLoading = true;
    this.errorMessage = '';
    const search = this.searchTerm.trim() || undefined;
    this.dispatchService.getDeliveries(search, this.isDeletedFilter).subscribe({
      next: ({ list, debts }) => {
        this.deliveries = list;
        this.debts = debts;
        this.isLoading = false;
        this.loadAvailability();
      },
      error: (error: any) => {
        this.isLoading = false;
        this.errorMessage =
          error?.errorMessage ||
          error?.error?.errorMessage ||
          this.localeService.translate('deliveries.loadFailed');
      }
    });
  }

  /** One ForAssign call per city on the page; a failing city just shows no status. */
  private loadAvailability(): void {
    const cityIds = [...new Set(this.deliveries.filter(d => !d.isDeleted && d.cityId).map(d => d.cityId))];
    if (!cityIds.length) {
      this.availability = new Map();
      return;
    }
    forkJoin(
      cityIds.map(id => this.dispatchService.getCandidatesForCity(id).pipe(catchError(() => of([] as RiderCandidate[]))))
    ).subscribe(lists => {
      const map = new Map<number, RiderCandidate>();
      for (const c of lists.flat()) map.set(c.deliveryId, c);
      this.availability = map;
    });
  }

  availabilityTone(deliveryId: number): 'ok' | 'warn' | 'off' {
    return riderStatusTone(this.availability.get(deliveryId)?.status);
  }

  availabilityKey(deliveryId: number): string {
    return riderStatusKey(this.availability.get(deliveryId)?.status);
  }

  applyFilters(): void {
    this.loadDeliveries();
  }

  clearFilters(): void {
    this.searchTerm = '';
    this.deletedFilterValue = 'active';
    this.loadDeliveries();
  }

  onAddNew(): void {
    this.router.navigate(['/main/deliveries/new']);
  }

  onEdit(delivery: DeliveryDto): void {
    if (delivery.isDeleted) return;
    this.router.navigate(['/main/deliveries', delivery.deliveryId, 'edit']);
  }

  onDelete(delivery: DeliveryDto): void {
    if (delivery.isDeleted || this.isDeleting) return;
    this.pendingDeleteId = delivery.deliveryId;
    this.showConfirmDialog = true;
  }

  onCancelDelete(): void {
    this.showConfirmDialog = false;
    this.pendingDeleteId = null;
  }

  onConfirmDelete(): void {
    if (this.pendingDeleteId == null || this.isDeleting) return;

    this.isDeleting = true;
    this.errorMessage = '';
    this.successMessage = '';
    this.deliveryClient.delete(this.pendingDeleteId).subscribe({
      next: () => {
        this.isDeleting = false;
        this.showConfirmDialog = false;
        this.pendingDeleteId = null;
        this.successMessage = this.localeService.translate('deliveries.deleteSuccess');
        this.loadDeliveries();
      },
      error: (error: any) => {
        this.isDeleting = false;
        this.showConfirmDialog = false;
        this.pendingDeleteId = null;
        this.errorMessage =
          error?.errorMessage ||
          error?.error?.errorMessage ||
          error?.result?.errorMessage ||
          this.localeService.translate('deliveries.deleteFailed');
      }
    });
  }

  // ── Cash debt ──────────────────────────────────────────────────────
  debtOf(deliveryId: number): RiderCashDebtInfo {
    return this.debts.get(deliveryId) ?? { cashDebt: 0, cashDebtLimit: null };
  }

  isOverDebtLimit(deliveryId: number): boolean {
    const d = this.debtOf(deliveryId);
    return isOverCashDebtLimit(d.cashDebt, d.cashDebtLimit);
  }

  money(value: number): string {
    return formatCashAmount(value);
  }

  onEditDebtLimit(delivery: DeliveryDto): void {
    if (delivery.isDeleted) return;
    this.limitTarget = delivery;
    const limit = this.debtOf(delivery.deliveryId).cashDebtLimit;
    this.limitInput = limit;
    this.limitError = '';
  }

  onCloseDebtLimit(): void {
    if (this.isSavingLimit) return;
    this.limitTarget = null;
    this.limitInput = null;
    this.limitError = '';
  }

  /** Empty = no limit. */
  private parsedLimit(): { ok: true; value: number | null } | { ok: false } {
    const raw = this.limitInput;
    if (raw === null || raw === undefined || String(raw).trim() === '') return { ok: true, value: null };
    const value = Number(raw);
    if (!Number.isFinite(value) || value < 0) return { ok: false };
    return { ok: true, value: Math.round(value * 100) / 100 };
  }

  onSaveDebtLimit(): void {
    if (!this.limitTarget || this.isSavingLimit) return;
    const parsed = this.parsedLimit();
    if (!parsed.ok) {
      this.limitError = this.localeService.translate('deliveries.debtLimitInvalid');
      return;
    }
    this.saveDebtLimit(parsed.value);
  }

  onRemoveDebtLimit(): void {
    if (!this.limitTarget || this.isSavingLimit) return;
    this.saveDebtLimit(null);
  }

  private saveDebtLimit(limit: number | null): void {
    const target = this.limitTarget;
    if (!target) return;
    this.isSavingLimit = true;
    this.limitError = '';
    this.dispatchService.setCashDebtLimit(target.deliveryId, limit).subscribe({
      next: () => {
        this.isSavingLimit = false;
        this.onCloseDebtLimit();
        this.successMessage = this.localeService.translate(
          limit == null ? 'deliveries.debtLimitRemoved' : 'deliveries.debtLimitSaved',
          { name: target.fullName }
        );
        this.loadDeliveries();
      },
      error: (error: any) => {
        this.isSavingLimit = false;
        this.limitError =
          error?.errorMessage ||
          error?.error?.errorMessage ||
          this.localeService.translate('deliveries.debtLimitSaveFailed');
      }
    });
  }

  statusLabel(isActive: boolean): string {
    return isActive
      ? this.localeService.translate('common.active')
      : this.localeService.translate('common.inactive');
  }
}
