import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { catchError, forkJoin, of } from 'rxjs';
import { AdminDeliveryClient, DeliveryDto } from '../../core/services/clientAPI';
import { LocaleService } from '../../core/services/locale.service';
import { RiderCandidate, RiderDispatchService } from '../../core/services/rider-dispatch.service';
import { riderStatusKey, riderStatusTone } from '../../shared/components/rider-picker/rider-picker.component';
import { TranslatePipe } from '../../shared/pipes/translate.pipe';
import { ConfirmDialogComponent } from '../../shared/components/confirm-dialog/confirm-dialog.component';
import {
  MultiSelectComponent,
  MultiSelectOption
} from '../../shared/components/multi-select/multi-select.component';

@Component({
  selector: 'app-deliveries',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, TranslatePipe, MultiSelectComponent, ConfirmDialogComponent],
  templateUrl: './deliveries.component.html',
  styleUrls: ['./deliveries.component.css']
})
export class DeliveriesComponent implements OnInit {
  private readonly localeService = inject(LocaleService);
  private readonly deliveryClient = inject(AdminDeliveryClient);
  private readonly router = inject(Router);
  private readonly dispatchService = inject(RiderDispatchService);

  deliveries: DeliveryDto[] = [];
  /** Live shift / online status per rider, from ForAssign by city. */
  availability = new Map<number, RiderCandidate>();
  searchTerm = '';
  deletedFilterValue: 'active' | 'deleted' = 'active';
  isLoading = false;
  isDeleting = false;
  pendingDeleteId: number | null = null;
  showConfirmDialog = false;
  errorMessage = '';
  successMessage = '';

  get deletedFilterOptions(): MultiSelectOption[] {
    return [
      { value: 'active', label: this.localeService.translate('deliveries.filterNotDeleted') },
      { value: 'deleted', label: this.localeService.translate('deliveries.filterDeleted') }
    ];
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
    this.deliveryClient.getAll(search, this.isDeletedFilter).subscribe({
      next: (list) => {
        this.deliveries = list || [];
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
          this.localeService.translate('deliveries.deleteFailed');
      }
    });
  }

  statusLabel(isActive: boolean): string {
    return isActive
      ? this.localeService.translate('common.active')
      : this.localeService.translate('common.inactive');
  }
}
