import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { Subject, debounceTime, filter, takeUntil } from 'rxjs';
import {
  AdminOrderClient,
  AdminAvailableVehicleItemDto,
  AdminReplacementOrderVehicleCommand,
  AdminRemoveOrderVehicleCommand,
  FaultParty,
  JournalDirection,
  LedgerPartyType,
  MarkMerchantHandoverToDeliveryCommand,
  MarkVehicleDeliveredToCustomerCommand,
  MarkVehicleDeliveredToOwnerCommand,
  MarkVehicleNotReceivedByCustomerCommand,
  MarkVehicleReceivedFromCustomerCommand,
  MarkVehicleReceivedFromOwnerCommand,
  MerchantClient,
  MerchantLookupDto,
  MerchantOrderResponseStatus,
  MerchantVehicleResponseStatus,
  OrderDetailDto,
  OrderJournalEntryKind,
  OrderState,
  OrderVehicleDto,
  PaymentMethod,
  PaymentState,
  ReassignMerchantOrderCommand,
  RefundState,
  SendOrderToMerchantsCommand,
  UpdateOrderStateCommand
} from '../../../core/services/clientAPI';
import { VehicleStatus } from '../../../core/enums/vehicle-status.enum';
import { LocaleService } from '../../../core/services/locale.service';
import { AdminNotificationService } from '../../../core/services/admin-notification.service';
import {
  CASH_DEBT_LIMIT_ERROR_PREFIX,
  DeliveryLeg,
  DeliveryPayoutLeg,
  HandoverImage,
  HandoverImagePosition,
  HandoverStep,
  LegAssignmentItem,
  OrderDispatchInfo,
  OrderLegRider,
  RiderAvailabilityStatus,
  RiderCandidate,
  RiderDispatchService
} from '../../../core/services/rider-dispatch.service';
import { RiderPickerComponent, riderStatusKey } from '../../../shared/components/rider-picker/rider-picker.component';
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import { VehicleSpecsComponent } from '../../../shared/components/vehicle-specs/vehicle-specs.component';
import {
  MultiSelectComponent,
  MultiSelectOption
} from '../../../shared/components/multi-select/multi-select.component';
import {
  VEHICLE_LIFECYCLE_STEPS,
  VehicleLifecycleStep,
  canMarkDeliveredToCustomer,
  canMarkDeliveredToOwner,
  canMarkReceivedFromCustomer,
  canMarkReceivedFromOwner,
  canMarkVehicleNotReceived,
  hasAnyReceivedFromOwner,
  isStepDone,
  nextLifecycleAction
} from '../../../shared/order-cycle/order-vehicle-cycle';
import { HasPermissionDirective } from '../../../shared/directives/has-permission.directive';

interface PipelineStep {
  state: OrderState;
  key: string;
}

/** One vehicle in the assign / reassign riders dialog. */
interface AssignLegDraft {
  vehicle: OrderVehicleDto;
  deliveryId: number | null;
  returnId: number | null;
  originalDeliveryId: number | null;
  originalReturnId: number | null;
  originalDeliveryName: string;
  originalReturnName: string;
  /** Translation key of why the leg cannot change, or null when it can. */
  deliveryLockKey: string | null;
  returnLockKey: string | null;
}

/** A changed leg going to a rider who is not online in a shift; listed in the confirm step. */
interface UnavailableAssignment {
  key: string;
  riderName: string;
  statusKey: string;
  legKey: string;
  vehicleLabel: string;
}

/** Order states in which each leg can still be (re)assigned. */
const DELIVERY_LEG_STATES: OrderState[] = [OrderState.Confirmed, OrderState.DeliveryAssigned, OrderState.OnWay];
const RETURN_LEG_STATES: OrderState[] = [...DELIVERY_LEG_STATES, OrderState.CustomerReceived];

const STEP_BY_LIFECYCLE: Record<VehicleLifecycleStep, HandoverStep> = {
  receivedFromOwner: HandoverStep.ReceivedFromOwner,
  deliveredToCustomer: HandoverStep.DeliveredToCustomer,
  receivedFromCustomer: HandoverStep.ReceivedFromCustomer,
  deliveredToOwner: HandoverStep.DeliveredToOwner
};

const STEP_LABEL_KEYS: Record<HandoverStep, string> = {
  [HandoverStep.ReceivedFromOwner]: 'orders.cyclePickup',
  [HandoverStep.DeliveredToCustomer]: 'orders.cycleDeliveredToCustomer',
  [HandoverStep.ReceivedFromCustomer]: 'orders.cycleReceivedFromCustomer',
  [HandoverStep.DeliveredToOwner]: 'orders.cycleReturnedToOwner'
};

const POSITION_LABEL_KEYS: Record<HandoverImagePosition, string> = {
  [HandoverImagePosition.Front]: 'orders.photoFront',
  [HandoverImagePosition.Back]: 'orders.photoBack',
  [HandoverImagePosition.Left]: 'orders.photoLeft',
  [HandoverImagePosition.Right]: 'orders.photoRight'
};

type OrderDetailTab = 'merchants' | 'riders' | 'journal' | 'documents';

@Component({
  selector: 'app-order-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, TranslatePipe, ConfirmDialogComponent, VehicleSpecsComponent, MultiSelectComponent, RiderPickerComponent, HasPermissionDirective],
  templateUrl: './order-detail.component.html',
  styleUrls: [
    './order-detail.component.css',
    '../../../shared/styles/entity-tiles.css'
  ]
})
export class OrderDetailComponent implements OnInit, OnDestroy {
  private readonly localeService = inject(LocaleService);
  private readonly merchantClient = inject(MerchantClient);
  private readonly dispatchService = inject(RiderDispatchService);
  private readonly adminNotifications = inject(AdminNotificationService);
  private readonly destroy$ = new Subject<void>();

  order: OrderDetailDto | null = null;
  /** Per-leg riders, handover photos and payout legs (not in the generated DTO yet). */
  dispatch: OrderDispatchInfo = { legs: [], handoverImages: [], payoutLegs: [] };
  orderId: number = 0;
  isLoading = false;
  errorMessage = '';
  successMessage = '';
  actionLoading: string = '';

  // Send to merchants
  showMerchantsModal = false;
  activeMerchants: MerchantLookupDto[] = [];
  selectedMerchantIds: number[] = [];
  isLoadingMerchants = false;

  // Reassign merchant
  showReassignModal = false;
  reassignOldMerchantId: number | null = null;
  reassignNewMerchantId: number | null = null;
  reassignMerchants: MerchantLookupDto[] = [];
  isLoadingReassign = false;

  // Replace vehicle
  showReplaceVehicleModal = false;
  replaceOldVehicleId: number | null = null;
  replaceNewVehicleId: number | null = null;
  replaceCandidates: AdminAvailableVehicleItemDto[] = [];
  isLoadingReplaceVehicles = false;
  showRemoveVehicleDialog = false;
  removeVehicleId: number | null = null;
  removeVehicleLoading = false;

  // Assign / reassign riders (delivery + return leg per vehicle)
  showDeliveryModal = false;
  assignDrafts: AssignLegDraft[] = [];
  riderCandidates: RiderCandidate[] = [];
  isLoadingDeliveries = false;
  /** Why the last save failed, shown inside the dialog (the page alert sits behind it). */
  assignError = '';
  /** Confirm step before giving trips to riders who are offline / off shift. */
  showUnavailableAssignConfirm = false;
  unavailableAssignments: UnavailableAssignment[] = [];

  // Handover photo viewer
  galleryImages: HandoverImage[] = [];
  galleryIndex = 0;
  galleryVehicle: OrderVehicleDto | null = null;
  galleryStep: HandoverStep | null = null;

  // Merchant handover
  showHandoverModal = false;
  selectedHandoverVehicleIds: number[] = [];

  readonly lifecycleSteps = VEHICLE_LIFECYCLE_STEPS;
  readonly operationalFaultParties: FaultParty[] = [
    FaultParty.Merchant,
    FaultParty.Delivery,
    FaultParty.Company
  ];

  get reassignMerchantOptions(): MultiSelectOption[] {
    return this.reassignMerchants
      .filter(m => m.merchantId != null)
      .map(m => ({
        value: m.merchantId as number,
        label: m.fullName || String(m.merchantId),
        description: m.mobileNumber || '—'
      }));
  }

  get replaceVehicleOptions(): MultiSelectOption[] {
    return this.replaceCandidates
      .filter(v => v.vehicleId != null)
      .map(v => ({
        value: v.vehicleId as number,
        label: `${v.name || ''} (${v.vehicleCode || ''})`.trim(),
        description: v.merchantName || '—'
      }));
  }

  get faultPartyOptions(): MultiSelectOption[] {
    return this.operationalFaultParties.map(party => ({
      value: party,
      label: this.getFaultPartyLabel(party)
    }));
  }

  get vehiclesByMerchant(): Array<{
    merchantId: number;
    merchantName: string;
    cashOnReceive: boolean;
    zoneName: string;
    vehicles: OrderVehicleDto[];
  }> {
    const groups = new Map<number, {
      merchantId: number;
      merchantName: string;
      cashOnReceive: boolean;
      zoneName: string;
      vehicles: OrderVehicleDto[];
    }>();
    for (const vehicle of this.order?.orderVehicles || []) {
      const merchantId = vehicle.merchantId || 0;
      const existing = groups.get(merchantId);
      if (existing) {
        existing.vehicles.push(vehicle);
        continue;
      }
      groups.set(merchantId, {
        merchantId,
        merchantName: (vehicle.merchantName || '').trim() || this.localeService.translate('orders.unassignedMerchant'),
        cashOnReceive: !!vehicle.merchantCashOnReceive,
        zoneName: (vehicle.merchantZoneName || '').trim(),
        vehicles: [vehicle]
      });
    }
    return [...groups.values()];
  }

  showLifecycleModal = false;
  lifecycleVehicle: OrderVehicleDto | null = null;
  lifecycleStep: VehicleLifecycleStep | null = null;
  lifecycleImage: string | null = null;
  lifecycleImageName: string | null = null;
  lifecycleDropActive = false;

  showVehicleNotReceivedModal = false;
  failVehicle: OrderVehicleDto | null = null;

  // Cancel / refund dialogs
  showCancelDialog = false;
  cancelDialogLoading = false;
  showRefundDialog = false;
  showCashDialog = false;
  cashDialogLoading = false;
  refundDialogLoading = false;
  showConfirmOrderDialog = false;

  readonly pipelineSteps: PipelineStep[] = [
    { state: OrderState.Pending, key: 'common.pending' },
    { state: OrderState.MerchantPending, key: 'common.merchantPending' },
    { state: OrderState.MerchantConfirmed, key: 'common.merchantConfirmed' },
    { state: OrderState.Confirmed, key: 'common.confirmed' },
    { state: OrderState.DeliveryAssigned, key: 'common.deliveryAssigned' },
    { state: OrderState.OnWay, key: 'common.onWay' },
    { state: OrderState.CustomerReceived, key: 'common.received' },
    { state: OrderState.Completed, key: 'common.completed' }
  ];

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private orderClient: AdminOrderClient
  ) {}

  ngOnInit(): void {
    this.route.params.pipe(takeUntil(this.destroy$)).subscribe(params => {
      this.orderId = +params['id'];
      if (this.orderId) {
        this.loadOrder();
      }
    });

    this.adminNotifications.incoming$.pipe(
      filter(n => !!n && !!this.orderId && n.orderId === this.orderId),
      debounceTime(300),
      takeUntil(this.destroy$)
    ).subscribe(() => this.loadOrder(true));
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadOrder(silent = false): void {
    if (!silent) {
      this.isLoading = true;
      this.errorMessage = '';
    }
    this.dispatchService.getOrderDetail(this.orderId).subscribe({
      next: ({ order, dispatch }) => {
        this.order = order;
        this.dispatch = dispatch;
        this.isLoading = false;
      },
      error: (error: any) => {
        if (!silent) {
          this.errorMessage = this.localeService.translate('orders.loadFailed');
          this.isLoading = false;
        }
        console.error('Error loading order:', error);
      }
    });
  }

  // ── Send to merchants ──────────────────────────────────────────────
  onOpenSendToMerchants(): void {
    if (!this.order) return;
    this.showMerchantsModal = true;
    this.selectedMerchantIds = [];
    this.activeMerchants = [];
    this.isLoadingMerchants = true;
    this.errorMessage = '';

    this.merchantClient.getActive().subscribe({
      next: (list) => {
        this.activeMerchants = list || [];
        this.isLoadingMerchants = false;
      },
      error: (error: any) => {
        this.showErrorMessage(
          error?.errorMessage || error?.error?.errorMessage || this.localeService.translate('orders.merchantsLoadFailed')
        );
        this.isLoadingMerchants = false;
      }
    });
  }

  toggleMerchantSelection(merchantId: number): void {
    const idx = this.selectedMerchantIds.indexOf(merchantId);
    if (idx > -1) {
      this.selectedMerchantIds = this.selectedMerchantIds.filter(id => id !== merchantId);
    } else {
      this.selectedMerchantIds = [...this.selectedMerchantIds, merchantId];
    }
  }

  isMerchantSelected(merchantId: number): boolean {
    return this.selectedMerchantIds.includes(merchantId);
  }

  onConfirmSendToMerchants(): void {
    if (!this.order || !this.selectedMerchantIds.length) {
      this.showErrorMessage(this.localeService.translate('orders.selectMerchantsError'));
      return;
    }

    this.actionLoading = 'sendMerchants';
    const command = new SendOrderToMerchantsCommand();
    command.orderId = this.orderId;
    command.merchantIds = [...this.selectedMerchantIds];

    this.orderClient.sendToMerchants(this.orderId, command).subscribe({
      next: () => {
        this.showMerchantsModal = false;
        this.showSuccessMessage(this.localeService.translate('orders.sendToMerchantsSuccess'));
        this.loadOrder();
        this.actionLoading = '';
      },
      error: (error: any) => {
        this.showErrorMessage(
          error?.errorMessage || error?.error?.errorMessage || this.localeService.translate('orders.sendToMerchantsFailed')
        );
        this.actionLoading = '';
      }
    });
  }

  onCloseMerchantsModal(): void {
    this.showMerchantsModal = false;
    this.selectedMerchantIds = [];
  }

  // ── Reassign merchant ──────────────────────────────────────────────
  onOpenReassign(oldMerchantId: number): void {
    this.reassignOldMerchantId = oldMerchantId;
    this.reassignNewMerchantId = null;
    this.showReassignModal = true;
    this.isLoadingReassign = true;
    this.reassignMerchants = [];

    this.merchantClient.getActive().subscribe({
      next: (list) => {
        this.reassignMerchants = (list || []).filter(m => m.merchantId !== oldMerchantId);
        this.isLoadingReassign = false;
      },
      error: (error: any) => {
        this.showErrorMessage(
          error?.errorMessage || error?.error?.errorMessage || this.localeService.translate('orders.merchantsLoadFailed')
        );
        this.isLoadingReassign = false;
      }
    });
  }

  onConfirmReassign(): void {
    if (!this.reassignOldMerchantId || !this.reassignNewMerchantId) {
      this.showErrorMessage(this.localeService.translate('orders.selectNewMerchantError'));
      return;
    }

    this.actionLoading = 'reassign';
    const command = new ReassignMerchantOrderCommand();
    command.orderId = this.orderId;
    command.oldMerchantId = this.reassignOldMerchantId;
    command.newMerchantId = this.reassignNewMerchantId;

    this.orderClient.reassignMerchant(this.orderId, command).subscribe({
      next: () => {
        this.showReassignModal = false;
        this.showSuccessMessage(this.localeService.translate('orders.reassignMerchantSuccess'));
        this.loadOrder();
        this.actionLoading = '';
      },
      error: (error: any) => {
        this.showErrorMessage(
          error?.errorMessage || error?.error?.errorMessage || this.localeService.translate('orders.reassignMerchantFailed')
        );
        this.actionLoading = '';
      }
    });
  }

  onCloseReassignModal(): void {
    this.showReassignModal = false;
    this.reassignOldMerchantId = null;
    this.reassignNewMerchantId = null;
  }

  canReplaceVehicle(): boolean {
    if (!this.order) return false;
    const state = this.order.orderState;
    return state === OrderState.Pending
      || state === OrderState.MerchantPending
      || state === OrderState.MerchantConfirmed;
  }

  canRemoveVehicle(vehicle: OrderVehicleDto): boolean {
    if (!this.canReplaceVehicle() || !this.order?.orderVehicles) return false;
    if (this.order.orderVehicles.length <= 1) return false;
    return vehicle.merchantResponseStatus === MerchantVehicleResponseStatus.Declined
      || this.order.orderState === OrderState.Pending
      || this.order.orderState === OrderState.MerchantPending
      || this.order.orderState === OrderState.MerchantConfirmed;
  }

  get hasDeclinedVehicles(): boolean {
    return (this.order?.orderVehicles || []).some(
      v => v.merchantResponseStatus === MerchantVehicleResponseStatus.Declined
    );
  }

  merchantVehicleResponseLabel(status: MerchantVehicleResponseStatus | undefined): string {
    switch (status) {
      case MerchantVehicleResponseStatus.Confirmed:
        return this.localeService.translate('orders.confirmedByMerchant');
      case MerchantVehicleResponseStatus.Declined:
        return this.localeService.translate('orders.declinedByMerchant');
      default:
        return this.localeService.translate('orders.awaitingMerchantVehicle');
    }
  }

  onOpenRemoveVehicle(vehicleId: number): void {
    this.removeVehicleId = vehicleId;
    this.showRemoveVehicleDialog = true;
  }

  onCancelRemoveVehicle(): void {
    this.showRemoveVehicleDialog = false;
    this.removeVehicleId = null;
    this.removeVehicleLoading = false;
  }

  onConfirmRemoveVehicle(): void {
    if (!this.removeVehicleId) return;
    this.removeVehicleLoading = true;
    const command = new AdminRemoveOrderVehicleCommand();
    command.orderId = this.orderId;
    command.vehicleId = this.removeVehicleId;
    this.orderClient.removeVehicle(this.orderId, command).subscribe({
      next: () => {
        this.showSuccessMessage(this.localeService.translate('orders.removeVehicleSuccess'));
        this.onCancelRemoveVehicle();
        this.loadOrder();
      },
      error: (error: any) => {
        this.removeVehicleLoading = false;
        this.showErrorMessage(
          error?.errorMessage || error?.error?.errorMessage || this.localeService.translate('orders.removeVehicleFailed')
        );
      }
    });
  }

  onOpenReplaceVehicle(oldVehicleId: number): void {
    if (!this.order) return;
    this.replaceOldVehicleId = oldVehicleId;
    this.replaceNewVehicleId = null;
    this.replaceCandidates = [];
    this.showReplaceVehicleModal = true;
    this.isLoadingReplaceVehicles = true;

    const currentIds = new Set((this.order.orderVehicles || []).map(v => v.vehicleId));
    this.orderClient.getAvailableVehicles(
      this.order.subCategoryId,
      this.order.cityId,
      this.order.reservationDateFrom,
      this.order.reservationDateTo,
      this.orderId,
      this.order.destinationZoneId
    ).subscribe({
      next: (result) => {
        this.replaceCandidates = (result?.vehicles || []).filter(
          v => v.isAvailable && !currentIds.has(v.vehicleId)
        );
        this.isLoadingReplaceVehicles = false;
      },
      error: (error: any) => {
        this.isLoadingReplaceVehicles = false;
        this.showErrorMessage(
          error?.errorMessage || error?.error?.errorMessage || this.localeService.translate('orders.replaceVehicleLoadFailed')
        );
      }
    });
  }

  onConfirmReplaceVehicle(): void {
    if (!this.replaceOldVehicleId || !this.replaceNewVehicleId) {
      this.showErrorMessage(this.localeService.translate('orders.selectNewVehicleError'));
      return;
    }

    this.actionLoading = 'replaceVehicle';
    const command = new AdminReplacementOrderVehicleCommand();
    command.orderId = this.orderId;
    command.oldVehicleId = this.replaceOldVehicleId;
    command.newVehicleId = this.replaceNewVehicleId;

    this.orderClient.replacement(this.orderId, command).subscribe({
      next: () => {
        this.showReplaceVehicleModal = false;
        this.showSuccessMessage(this.localeService.translate('orders.replaceVehicleSuccess'));
        this.actionLoading = '';
        this.loadOrder();
      },
      error: (error: any) => {
        this.actionLoading = '';
        this.showErrorMessage(
          error?.errorMessage || error?.error?.errorMessage || this.localeService.translate('orders.replaceVehicleFailed')
        );
      }
    });
  }

  onCloseReplaceVehicleModal(): void {
    this.showReplaceVehicleModal = false;
    this.replaceOldVehicleId = null;
    this.replaceNewVehicleId = null;
    this.replaceCandidates = [];
  }

  // ── Confirm (MerchantConfirmed → Confirmed, no vehicles) ───────────
  onConfirmOrder(): void {
    if (!this.order) return;
    this.errorMessage = '';
    this.successMessage = '';
    this.showConfirmOrderDialog = true;
  }

  onCancelConfirmOrderDialog(): void {
    this.showConfirmOrderDialog = false;
  }

  onSubmitConfirmOrder(): void {
    if (!this.order) return;

    this.actionLoading = 'confirm';
    const command = new UpdateOrderStateCommand();
    command.orderId = this.orderId;
    command.newState = OrderState.Confirmed;
    command.vehicleIds = null;

    this.orderClient.updateOrderState(this.orderId, command).subscribe({
      next: () => {
        this.showConfirmOrderDialog = false;
        this.showSuccessMessage(this.localeService.translate('orders.confirmedSuccess'));
        this.loadOrder();
        this.actionLoading = '';
      },
      error: (error: any) => {
        this.showConfirmOrderDialog = false;
        this.showErrorMessage(
          error?.errorMessage
          || error?.error?.errorMessage
          || error?.result?.errorMessage
          || this.localeService.translate('orders.confirmFailed')
        );
        this.actionLoading = '';
      }
    });
  }

  /** Rider holding the delivery trip (merchant → customer). Rows without a leg are delivery. */
  deliveryFor(vehicleId: number): OrderLegRider | undefined {
    return this.dispatch.legs.find(d => d.vehicleId === vehicleId && d.leg === DeliveryLeg.Delivery);
  }

  /** Rider holding the return trip (customer → merchant). */
  returnFor(vehicleId: number): OrderLegRider | undefined {
    return this.dispatch.legs.find(d => d.vehicleId === vehicleId && d.leg === DeliveryLeg.Return);
  }

  hasAnyRider(vehicleId: number): boolean {
    return !!this.deliveryFor(vehicleId) || !!this.returnFor(vehicleId);
  }

  /** Why the delivery trip of this vehicle can no longer change, or null when it can. */
  deliveryLegLockKey(vehicle: OrderVehicleDto): string | null {
    if (!this.order) return 'orders.legStateLocked';
    if (vehicle.receivedFromOwner || this.deliveryFor(vehicle.vehicleId)?.deliveryReceivedFromMerchant) {
      return 'orders.legDeliveryStarted';
    }
    return DELIVERY_LEG_STATES.includes(this.order.orderState) ? null : 'orders.legStateLocked';
  }

  /** Why the return trip of this vehicle can no longer change, or null when it can. */
  returnLegLockKey(vehicle: OrderVehicleDto): string | null {
    if (!this.order) return 'orders.legStateLocked';
    if (vehicle.receivedFromCustomer) return 'orders.legReturnStarted';
    return RETURN_LEG_STATES.includes(this.order.orderState) ? null : 'orders.legStateLocked';
  }

  canAssignVehicle(vehicle: OrderVehicleDto): boolean {
    if (!this.order || this.isCancelled) return false;
    if (vehicle.merchantResponseStatus === MerchantVehicleResponseStatus.Declined) return false;
    if (vehicle.deliveryFailed) return false;
    return this.deliveryLegLockKey(vehicle) === null || this.returnLegLockKey(vehicle) === null;
  }

  get canAssignAnyVehicle(): boolean {
    return this.assignableVehicles.some(v => this.canAssignVehicle(v));
  }

  get hasAnyLegRider(): boolean {
    return this.dispatch.legs.length > 0;
  }

  assignedVehicleCount(): number {
    return this.assignableVehicles.filter(v => !!this.deliveryFor(v.vehicleId)).length;
  }

  get assignableVehicles(): OrderVehicleDto[] {
    return (this.order?.orderVehicles || []).filter(
      v => v.merchantResponseStatus !== MerchantVehicleResponseStatus.Declined
    );
  }

  // ── Assign / reassign riders ───────────────────────────────────────
  /** Opens the dialog for one vehicle, or for every vehicle that still has a changeable leg. */
  onOpenAssignDelivery(vehicle?: OrderVehicleDto): void {
    if (!this.order) return;
    const vehicles = (vehicle ? [vehicle] : this.assignableVehicles).filter(v => this.canAssignVehicle(v));
    if (!vehicles.length) return;

    this.assignDrafts = vehicles.map(v => {
      const delivery = this.deliveryFor(v.vehicleId);
      const ret = this.returnFor(v.vehicleId);
      return {
        vehicle: v,
        deliveryId: delivery?.deliveryId ?? null,
        returnId: ret?.deliveryId ?? null,
        originalDeliveryId: delivery?.deliveryId ?? null,
        originalReturnId: ret?.deliveryId ?? null,
        originalDeliveryName: delivery?.deliveryName || '',
        originalReturnName: ret?.deliveryName || '',
        deliveryLockKey: this.deliveryLegLockKey(v),
        returnLockKey: this.returnLegLockKey(v)
      };
    });
    this.riderCandidates = [];
    this.assignError = '';
    this.showDeliveryModal = true;
    this.isLoadingDeliveries = true;

    this.dispatchService.getCandidatesForOrder(this.orderId).subscribe({
      next: (list) => {
        this.riderCandidates = list;
        this.isLoadingDeliveries = false;
      },
      error: (error: any) => {
        this.showErrorMessage(
          error?.errorMessage || error?.error?.errorMessage || this.localeService.translate('orders.deliveriesLoadFailed')
        );
        this.isLoadingDeliveries = false;
      }
    });
  }

  /** The rider who collects the cash (delivery trip) is refused at his debt limit on cash orders. */
  get isCashOrder(): boolean {
    return this.order?.paymentMethod === PaymentMethod.Cash;
  }

  private candidateById(deliveryId: number | null): RiderCandidate | undefined {
    return deliveryId == null ? undefined : this.riderCandidates.find(c => c.deliveryId === deliveryId);
  }

  /**
   * Inline warning under a picker whose newly picked rider is not online in a shift. Riders who
   * already hold the leg are not warned about.
   */
  riderAvailabilityWarningKey(deliveryId: number | null, originalId: number | null): string | null {
    if (deliveryId == null || deliveryId === originalId) return null;
    switch (this.candidateById(deliveryId)?.status) {
      case RiderAvailabilityStatus.InShiftOffline:
        return 'orders.riderOfflineWarning';
      case RiderAvailabilityStatus.OffShift:
        return 'orders.riderOffShiftWarning';
      default:
        return null;
    }
  }

  onDraftDeliveryChange(draft: AssignLegDraft, deliveryId: number): void {
    this.assignError = '';
    draft.deliveryId = deliveryId;
    // A fresh vehicle gets the same rider on both trips, like the old single-rider flow.
    if (!draft.returnLockKey && draft.originalReturnId == null && draft.returnId == null) {
      draft.returnId = deliveryId;
    }
  }

  onDraftReturnChange(draft: AssignLegDraft, deliveryId: number): void {
    this.assignError = '';
    draft.returnId = deliveryId;
  }

  canUseSameRiderForReturn(draft: AssignLegDraft): boolean {
    return !draft.returnLockKey && draft.deliveryId != null && draft.returnId !== draft.deliveryId;
  }

  useSameRiderForReturn(draft: AssignLegDraft): void {
    if (this.canUseSameRiderForReturn(draft)) {
      this.assignError = '';
      draft.returnId = draft.deliveryId;
    }
  }

  /** Only the (vehicle, leg) pairs the admin actually changed. */
  get pendingAssignments(): LegAssignmentItem[] {
    const items: LegAssignmentItem[] = [];
    for (const draft of this.assignDrafts) {
      if (!draft.deliveryLockKey && draft.deliveryId != null && draft.deliveryId !== draft.originalDeliveryId) {
        items.push({ vehicleId: draft.vehicle.vehicleId, deliveryId: draft.deliveryId, leg: DeliveryLeg.Delivery });
      }
      if (!draft.returnLockKey && draft.returnId != null && draft.returnId !== draft.originalReturnId) {
        items.push({ vehicleId: draft.vehicle.vehicleId, deliveryId: draft.returnId, leg: DeliveryLeg.Return });
      }
    }
    return items;
  }

  get assignDialogIsReassign(): boolean {
    return this.assignDrafts.some(d => d.originalDeliveryId != null || d.originalReturnId != null);
  }

  /** Changed legs whose new rider is offline or off shift right now. */
  private collectUnavailableAssignments(assignments: LegAssignmentItem[]): UnavailableAssignment[] {
    const result: UnavailableAssignment[] = [];
    for (const item of assignments) {
      const rider = this.candidateById(item.deliveryId);
      if (!rider || rider.status === RiderAvailabilityStatus.Available) continue;
      const vehicle = this.assignDrafts.find(d => d.vehicle.vehicleId === item.vehicleId)?.vehicle;
      result.push({
        key: `${item.vehicleId}-${item.leg}`,
        riderName: rider.fullName,
        statusKey: riderStatusKey(rider.status),
        legKey: item.leg === DeliveryLeg.Return ? 'orders.legReturn' : 'orders.legDelivery',
        vehicleLabel: vehicle ? `${vehicle.vehicleName} · #${vehicle.vehicleCode}` : `#${item.vehicleId}`
      });
    }
    return result;
  }

  onConfirmAssignDelivery(): void {
    const assignments = this.pendingAssignments;
    if (!this.order || !assignments.length) {
      this.showErrorMessage(this.localeService.translate('orders.assignNoChanges'));
      return;
    }

    const unavailable = this.collectUnavailableAssignments(assignments);
    if (unavailable.length) {
      this.unavailableAssignments = unavailable;
      this.showUnavailableAssignConfirm = true;
      return;
    }
    this.submitAssignments(assignments);
  }

  onConfirmUnavailableAssign(): void {
    this.showUnavailableAssignConfirm = false;
    this.unavailableAssignments = [];
    this.submitAssignments(this.pendingAssignments);
  }

  onCancelUnavailableAssign(): void {
    this.showUnavailableAssignConfirm = false;
    this.unavailableAssignments = [];
  }

  trackUnavailable(_: number, item: UnavailableAssignment): string {
    return item.key;
  }

  private submitAssignments(assignments: LegAssignmentItem[]): void {
    if (!assignments.length) return;
    this.assignError = '';
    this.actionLoading = 'assignDelivery';
    this.dispatchService.assignLegs(this.orderId, assignments).subscribe({
      next: () => {
        this.onCloseDeliveryModal();
        this.showSuccessMessage(this.localeService.translate('orders.assignRidersSuccess'));
        this.loadOrder();
        this.actionLoading = '';
      },
      error: (error: any) => {
        const message: string =
          error?.errorMessage || error?.error?.errorMessage || error?.result?.errorMessage || '';
        this.assignError = this.assignErrorText(message);
        this.actionLoading = '';
        // Debts moved since the dialog opened; refresh the chips so the blocked rider shows as such.
        if (message.startsWith(CASH_DEBT_LIMIT_ERROR_PREFIX)) {
          this.refreshRiderCandidates();
        }
      }
    });
  }

  /** The cash-debt refusal is in English from the backend; keep its rider list, translate the rest. */
  private assignErrorText(message: string): string {
    if (message.startsWith(CASH_DEBT_LIMIT_ERROR_PREFIX)) {
      const colon = message.indexOf(':');
      const riders = colon >= 0 ? message.slice(colon + 1).trim() : '';
      return riders
        ? this.localeService.translate('orders.assignCashDebtLimitError', { riders })
        : this.localeService.translate('orders.assignCashDebtLimitErrorShort');
    }
    return message || this.localeService.translate('orders.assignDeliveryFailed');
  }

  private refreshRiderCandidates(): void {
    this.dispatchService.getCandidatesForOrder(this.orderId).subscribe({
      next: (list) => {
        if (this.showDeliveryModal) this.riderCandidates = list;
      },
      error: () => {}
    });
  }

  onCloseDeliveryModal(): void {
    this.showDeliveryModal = false;
    this.showUnavailableAssignConfirm = false;
    this.unavailableAssignments = [];
    this.assignError = '';
    this.assignDrafts = [];
    this.riderCandidates = [];
  }

  // ── Handover photos ────────────────────────────────────────────────
  stepImages(vehicle: OrderVehicleDto, step: VehicleLifecycleStep): HandoverImage[] {
    const handoverStep = STEP_BY_LIFECYCLE[step];
    return this.dispatch.handoverImages
      .filter(i => i.vehicleId === vehicle.vehicleId && i.step === handoverStep)
      .sort((a, b) => a.position - b.position);
  }

  openGallery(vehicle: OrderVehicleDto, step: VehicleLifecycleStep, index = 0): void {
    const images = this.stepImages(vehicle, step);
    if (!images.length) return;
    this.galleryImages = images;
    this.galleryIndex = Math.min(index, images.length - 1);
    this.galleryVehicle = vehicle;
    this.galleryStep = STEP_BY_LIFECYCLE[step];
  }

  closeGallery(): void {
    this.galleryImages = [];
    this.galleryIndex = 0;
    this.galleryVehicle = null;
    this.galleryStep = null;
  }

  get galleryImage(): HandoverImage | null {
    return this.galleryImages[this.galleryIndex] ?? null;
  }

  stepLabelKey(step: HandoverStep | null): string {
    return step ? STEP_LABEL_KEYS[step] : '';
  }

  positionLabelKey(position: HandoverImagePosition): string {
    return POSITION_LABEL_KEYS[position] || 'orders.cycleProofImage';
  }

  /** Name of the rider who took a photo, from the order's leg riders. */
  photoTakenBy(image: HandoverImage | null): string {
    if (!image?.deliveryId) return '';
    return this.dispatch.legs.find(l => l.deliveryId === image.deliveryId)?.deliveryName || '';
  }

  // ── Delivery payouts ───────────────────────────────────────────────
  payoutLeg(deliveryOrderPaymentDetailId: number): DeliveryPayoutLeg | undefined {
    return this.dispatch.payoutLegs.find(p => p.deliveryOrderPaymentDetailId === deliveryOrderPaymentDetailId);
  }

  legLabelKey(leg: DeliveryLeg | undefined): string {
    return leg === DeliveryLeg.Return ? 'orders.legReturn' : 'orders.legDelivery';
  }

  // ── Merchant handover ──────────────────────────────────────────────
  get unreceivedDeliveryVehicles(): OrderLegRider[] {
    return this.dispatch.legs.filter(d => d.leg === DeliveryLeg.Delivery && !d.deliveryReceivedFromMerchant);
  }

  onOpenHandover(): void {
    this.selectedHandoverVehicleIds = [];
    this.showHandoverModal = true;
  }

  toggleHandoverVehicle(vehicleId: number): void {
    const idx = this.selectedHandoverVehicleIds.indexOf(vehicleId);
    if (idx > -1) {
      this.selectedHandoverVehicleIds = this.selectedHandoverVehicleIds.filter(id => id !== vehicleId);
    } else {
      this.selectedHandoverVehicleIds = [...this.selectedHandoverVehicleIds, vehicleId];
    }
  }

  isHandoverSelected(vehicleId: number): boolean {
    return this.selectedHandoverVehicleIds.includes(vehicleId);
  }

  onConfirmHandover(): void {
    if (!this.selectedHandoverVehicleIds.length) {
      this.showErrorMessage(this.localeService.translate('orders.selectHandoverVehiclesError'));
      return;
    }

    this.actionLoading = 'handover';
    const command = new MarkMerchantHandoverToDeliveryCommand();
    command.orderId = this.orderId;
    command.vehicleIds = [...this.selectedHandoverVehicleIds];

    this.orderClient.markMerchantHandover(this.orderId, command).subscribe({
      next: () => {
        this.showHandoverModal = false;
        this.showSuccessMessage(this.localeService.translate('orders.markHandoverSuccess'));
        this.loadOrder();
        this.actionLoading = '';
      },
      error: (error: any) => {
        this.showErrorMessage(
          error?.errorMessage || error?.error?.errorMessage || this.localeService.translate('orders.markHandoverFailed')
        );
        this.actionLoading = '';
      }
    });
  }

  onCloseHandoverModal(): void {
    this.showHandoverModal = false;
    this.selectedHandoverVehicleIds = [];
  }

  isCycleStepDone(vehicle: OrderVehicleDto, step: VehicleLifecycleStep): boolean {
    return isStepDone(vehicle, step);
  }

  cycleProofUrl(vehicle: OrderVehicleDto, step: VehicleLifecycleStep): string | null {
    switch (step) {
      case 'receivedFromOwner':
        return vehicle.receivedFromOwnerImageUrl;
      case 'deliveredToCustomer':
        return vehicle.deliveredToCustomerImageUrl;
      case 'receivedFromCustomer':
        return vehicle.receivedFromCustomerImageUrl;
      case 'deliveredToOwner':
        return vehicle.deliveredToOwnerImageUrl;
    }
  }

  nextVehicleAction(vehicle: OrderVehicleDto): VehicleLifecycleStep | null {
    if (!this.order) return null;
    return nextLifecycleAction(this.order.orderState, vehicle, this.isCancelled);
  }

  canPickupVehicle(vehicle: OrderVehicleDto): boolean {
    return !!this.order && canMarkReceivedFromOwner(this.order.orderState, vehicle, this.isCancelled);
  }

  canDeliverToCustomer(vehicle: OrderVehicleDto): boolean {
    return !!this.order && canMarkDeliveredToCustomer(this.order.orderState, vehicle, this.isCancelled);
  }

  canReceiveFromCustomer(vehicle: OrderVehicleDto): boolean {
    return !!this.order && canMarkReceivedFromCustomer(this.order.orderState, vehicle, this.isCancelled);
  }

  canReturnToOwner(vehicle: OrderVehicleDto): boolean {
    return !!this.order && canMarkDeliveredToOwner(this.order.orderState, vehicle, this.isCancelled);
  }

  canFailVehicle(vehicle: OrderVehicleDto): boolean {
    return !!this.order && canMarkVehicleNotReceived(this.order.orderState, vehicle, this.isCancelled);
  }

  lifecycleActionLabel(step: VehicleLifecycleStep): string {
    const map: Record<VehicleLifecycleStep, string> = {
      receivedFromOwner: 'orders.markPickup',
      deliveredToCustomer: 'orders.markDeliveredToCustomer',
      receivedFromCustomer: 'orders.markReceivedFromCustomer',
      deliveredToOwner: 'orders.markReturnedToOwner'
    };
    return this.localeService.translate(map[step]);
  }

  openLifecycleModal(vehicle: OrderVehicleDto, step: VehicleLifecycleStep): void {
    this.lifecycleVehicle = vehicle;
    this.lifecycleStep = step;
    this.lifecycleImage = null;
    this.lifecycleImageName = null;
    this.lifecycleDropActive = false;
    this.showLifecycleModal = true;
  }

  onLifecycleImageSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (file) {
      this.setLifecycleImageFile(file);
    }
    input.value = '';
  }

  onLifecycleDragOver(event: DragEvent): void {
    event.preventDefault();
    this.lifecycleDropActive = true;
  }

  onLifecycleDragLeave(event: DragEvent): void {
    event.preventDefault();
    this.lifecycleDropActive = false;
  }

  onLifecycleDrop(event: DragEvent): void {
    event.preventDefault();
    this.lifecycleDropActive = false;
    const file = event.dataTransfer?.files?.[0];
    if (file) {
      this.setLifecycleImageFile(file);
    }
  }

  private setLifecycleImageFile(file: File): void {
    if (!file.type.startsWith('image/')) {
      this.showErrorMessage(this.localeService.translate('orders.passportInvalidType'));
      return;
    }
    if (file.size > 10 * 1024 * 1024) {
      this.showErrorMessage(this.localeService.translate('orders.cycleProofTooLarge'));
      return;
    }
    const reader = new FileReader();
    reader.onload = () => {
      this.lifecycleImage = typeof reader.result === 'string' ? reader.result : null;
      this.lifecycleImageName = file.name;
    };
    reader.readAsDataURL(file);
  }

  clearLifecycleImage(): void {
    this.lifecycleImage = null;
    this.lifecycleImageName = null;
  }

  onCloseLifecycleModal(): void {
    this.showLifecycleModal = false;
    this.lifecycleVehicle = null;
    this.lifecycleStep = null;
    this.lifecycleImage = null;
    this.lifecycleImageName = null;
    this.lifecycleDropActive = false;
  }

  onConfirmLifecycle(): void {
    if (!this.order || !this.lifecycleVehicle || !this.lifecycleStep) return;
    const vehicleId = this.lifecycleVehicle.vehicleId;
    const step = this.lifecycleStep;
    this.actionLoading = `cycle-${vehicleId}-${step}`;

    const imageUrl = this.lifecycleImage;
    let request$;
    switch (step) {
      case 'receivedFromOwner': {
        const command = new MarkVehicleReceivedFromOwnerCommand();
        command.orderId = this.orderId;
        command.vehicleId = vehicleId;
        command.imageUrl = imageUrl;
        request$ = this.orderClient.markVehicleReceivedFromOwner(this.orderId, vehicleId, command);
        break;
      }
      case 'deliveredToCustomer': {
        const command = new MarkVehicleDeliveredToCustomerCommand();
        command.orderId = this.orderId;
        command.vehicleId = vehicleId;
        command.imageUrl = imageUrl;
        request$ = this.orderClient.markVehicleDeliveredToCustomer(this.orderId, vehicleId, command);
        break;
      }
      case 'receivedFromCustomer': {
        const command = new MarkVehicleReceivedFromCustomerCommand();
        command.orderId = this.orderId;
        command.vehicleId = vehicleId;
        command.imageUrl = imageUrl;
        request$ = this.orderClient.markVehicleReceivedFromCustomer(this.orderId, vehicleId, command);
        break;
      }
      case 'deliveredToOwner': {
        const command = new MarkVehicleDeliveredToOwnerCommand();
        command.orderId = this.orderId;
        command.vehicleId = vehicleId;
        command.imageUrl = imageUrl;
        request$ = this.orderClient.markVehicleDeliveredToOwner(this.orderId, vehicleId, command);
        break;
      }
      default:
        this.actionLoading = '';
        return;
    }

    request$.subscribe({
      next: () => {
        this.onCloseLifecycleModal();
        this.showSuccessMessage(this.localeService.translate('orders.vehicleCycleSuccess'));
        this.loadOrder();
        this.actionLoading = '';
      },
      error: (error: any) => {
        this.showErrorMessage(
          error?.errorMessage || error?.error?.errorMessage || this.localeService.translate('orders.vehicleCycleFailed')
        );
        this.actionLoading = '';
      }
    });
  }

  openVehicleNotReceived(vehicle: OrderVehicleDto): void {
    this.failVehicle = vehicle;
    this.showVehicleNotReceivedModal = true;
  }

  onCloseVehicleNotReceived(): void {
    this.showVehicleNotReceivedModal = false;
    this.failVehicle = null;
  }

  onConfirmVehicleNotReceived(): void {
    if (!this.failVehicle) return;
    this.actionLoading = 'vehicleNotReceived';
    const command = new MarkVehicleNotReceivedByCustomerCommand();
    command.orderId = this.orderId;
    command.vehicleId = this.failVehicle.vehicleId;
    this.orderClient.markVehicleNotReceivedByCustomer(this.orderId, this.failVehicle.vehicleId, command).subscribe({
      next: () => {
        this.onCloseVehicleNotReceived();
        this.showSuccessMessage(this.localeService.translate('orders.vehicleCancelledSuccess'));
        this.loadOrder();
        this.actionLoading = '';
      },
      error: (error: any) => {
        this.showErrorMessage(
          error?.errorMessage || error?.error?.errorMessage || this.localeService.translate('orders.vehicleCancelledFailed')
        );
        this.actionLoading = '';
      }
    });
  }

  // ── Cancellation ───────────────────────────────────────────────────
  onCancelOrder(): void {
    if (!this.order) return;
    this.errorMessage = '';
    this.successMessage = '';
    this.showCancelDialog = true;
  }

  onConfirmCancelOrder(): void {
    this.cancelDialogLoading = true;
    this.actionLoading = 'cancel';
    this.orderClient.rejectOrder(this.orderId).subscribe({
      next: () => {
        this.showCancelDialog = false;
        this.cancelDialogLoading = false;
        this.showSuccessMessage(this.localeService.translate('orders.cancelledSuccess'));
        this.loadOrder();
        this.actionLoading = '';
      },
      error: (error: any) => {
        this.showCancelDialog = false;
        this.cancelDialogLoading = false;
        this.showErrorMessage(
          error?.errorMessage || error?.error?.errorMessage || this.localeService.translate('orders.rejectFailed')
        );
        this.actionLoading = '';
      }
    });
  }

  onCancelCancelDialog(): void {
    this.showCancelDialog = false;
  }

  onMarkCancellationFeePaid(): void {
    if (!this.order?.orderCancellationFee) return;
    this.errorMessage = '';
    this.successMessage = '';
    this.actionLoading = 'markFeePaid';
    this.orderClient.markOrderCancellationFeePaid(this.orderId).subscribe({
      next: () => {
        this.showSuccessMessage(this.localeService.translate('orders.feePaidSuccess'));
        this.loadOrder();
        this.actionLoading = '';
      },
      error: (error: any) => {
        this.showErrorMessage(
          error?.errorMessage || error?.error?.errorMessage || this.localeService.translate('orders.feePaidFailed')
        );
        this.actionLoading = '';
      }
    });
  }

  canMarkMoneyRefunded(): boolean {
    if (!this.order?.refundablePaypalAmount || this.order.moneyRefunded) return false;
    return this.order.refundablePaypalAmount.state === RefundState.Pending;
  }

  onMarkMoneyRefunded(): void {
    if (!this.canMarkMoneyRefunded()) return;
    this.errorMessage = '';
    this.successMessage = '';
    this.showRefundDialog = true;
  }

  onConfirmMarkMoneyRefunded(): void {
    this.refundDialogLoading = true;
    this.actionLoading = 'markRefunded';
    this.orderClient.markMoneyRefunded(this.orderId).subscribe({
      next: () => {
        this.showRefundDialog = false;
        this.refundDialogLoading = false;
        this.showSuccessMessage(this.localeService.translate('orders.refundMarkedSuccess'));
        this.loadOrder();
        this.actionLoading = '';
      },
      error: (error: any) => {
        this.showRefundDialog = false;
        this.refundDialogLoading = false;
        this.showErrorMessage(
          error?.errorMessage || error?.error?.errorMessage || this.localeService.translate('orders.refundMarkedFailed')
        );
        this.actionLoading = '';
      }
    });
  }

  onCancelRefundDialog(): void {
    this.showRefundDialog = false;
  }

  /** Online order whose payment never went through, before any vehicle reached the customer. */
  canChangeToCash(): boolean {
    const o = this.order;
    if (!o || o.paymentMethod === PaymentMethod.Cash) return false;
    if (this.isCancelled || o.orderState === OrderState.Completed) return false;
    if (o.orderTotalDebitedToCompany) return false;
    if ((o.orderPayments || []).some(p => p.state === PaymentState.Paid || p.state === PaymentState.Refunded)) return false;
    return !(o.orderVehicles || []).some(v => v.deliveredToCustomer);
  }

  onChangeToCash(): void {
    if (!this.canChangeToCash()) return;
    this.errorMessage = '';
    this.successMessage = '';
    this.showCashDialog = true;
  }

  onConfirmChangeToCash(): void {
    this.cashDialogLoading = true;
    this.actionLoading = 'changeToCash';
    this.dispatchService.changeOrderToCash(this.orderId).subscribe({
      next: () => {
        this.showCashDialog = false;
        this.cashDialogLoading = false;
        this.actionLoading = '';
        this.showSuccessMessage(this.localeService.translate('orders.changedToCash'));
        this.loadOrder();
      },
      error: (error: any) => {
        this.showCashDialog = false;
        this.cashDialogLoading = false;
        this.actionLoading = '';
        this.showErrorMessage(
          error?.errorMessage || error?.error?.errorMessage || this.localeService.translate('orders.changeToCashFailed')
        );
      }
    });
  }

  onBack(): void {
    this.router.navigate(['/main/orders']);
  }

  onGoSettlements(): void {
    this.router.navigate(['/main/settlements']);
  }

  // ── Labels / helpers ───────────────────────────────────────────────
  getStateLabel(state: OrderState): string {
    switch (state) {
      case OrderState.Pending:
        return this.localeService.translate('common.pending');
      case OrderState.MerchantPending:
        return this.localeService.translate('common.merchantPending');
      case OrderState.MerchantConfirmed:
        return this.localeService.translate('common.merchantConfirmed');
      case OrderState.Confirmed:
        return this.localeService.translate('common.confirmed');
      case OrderState.DeliveryAssigned:
        return this.localeService.translate('common.deliveryAssigned');
      case OrderState.OnWay:
        return this.localeService.translate('common.onWay');
      case OrderState.CustomerReceived:
        return this.localeService.translate('common.received');
      case OrderState.CustomerRejectedReceipt:
        return this.localeService.translate('common.rejectedReceipt');
      case OrderState.Completed:
        return this.localeService.translate('common.completed');
      case OrderState.Cancelled:
        return this.localeService.translate('orders.cancelled');
      default:
        return this.localeService.translate('common.noData');
    }
  }

  getStateClass(state: OrderState): string {
    switch (state) {
      case OrderState.Pending:
        return 'od__status--pending';
      case OrderState.MerchantPending:
        return 'od__status--merchant-pending';
      case OrderState.MerchantConfirmed:
        return 'od__status--merchant-confirmed';
      case OrderState.Confirmed:
        return 'od__status--confirmed';
      case OrderState.DeliveryAssigned:
        return 'od__status--delivery-assigned';
      case OrderState.OnWay:
        return 'od__status--onway';
      case OrderState.CustomerReceived:
        return 'od__status--received';
      case OrderState.CustomerRejectedReceipt:
        return 'od__status--rejected-receipt';
      case OrderState.Completed:
        return 'od__status--completed';
      case OrderState.Cancelled:
        return 'od__status--cancelled';
      default:
        return '';
    }
  }

  deliveryAssignmentChip(assignment: OrderLegRider): { labelKey: string; kind: 'ok' | 'warn' | 'soft' } {
    const vehicle = this.order?.orderVehicles?.find(v => v.vehicleId === assignment.vehicleId);
    if (vehicle?.deliveredToOwner) {
      return { labelKey: 'orders.cycleReturnedToOwner', kind: 'ok' };
    }
    if (vehicle?.receivedFromCustomer) {
      return { labelKey: 'orders.cycleReceivedFromCustomer', kind: 'ok' };
    }
    if (vehicle?.deliveredToCustomer) {
      return { labelKey: 'orders.cycleDeliveredToCustomer', kind: 'ok' };
    }
    if (assignment.deliveryReceivedFromMerchant) {
      return { labelKey: 'orders.handoverDone', kind: 'ok' };
    }
    return { labelKey: 'orders.handoverPending', kind: 'warn' };
  }

  getMerchantStatusLabel(status: MerchantOrderResponseStatus): string {
    switch (status) {
      case MerchantOrderResponseStatus.Pending:
        return this.localeService.translate('common.pending');
      case MerchantOrderResponseStatus.Accepted:
        return this.localeService.translate('orders.merchantAccepted');
      case MerchantOrderResponseStatus.Rejected:
        return this.localeService.translate('orders.merchantRejected');
      case MerchantOrderResponseStatus.PartiallyAccepted:
        return this.localeService.translate('orders.merchantPartial');
      default:
        return this.localeService.translate('common.noData');
    }
  }

  getFaultPartyLabel(party: FaultParty | null | undefined): string {
    switch (party) {
      case FaultParty.Customer:
        return this.localeService.translate('orders.faultCustomer');
      case FaultParty.Merchant:
        return this.localeService.translate('orders.faultMerchant');
      case FaultParty.Delivery:
        return this.localeService.translate('orders.faultDelivery');
      case FaultParty.Company:
        return this.localeService.translate('orders.faultCompany');
      default:
        return this.localeService.translate('common.noData');
    }
  }

  getJournalDirectionLabel(direction: JournalDirection): string {
    return direction === JournalDirection.Debit
      ? this.localeService.translate('orders.journalDebit')
      : this.localeService.translate('orders.journalCredit');
  }

  getJournalKindLabel(kind: OrderJournalEntryKind): string {
    const key = `orders.journalKind.${OrderJournalEntryKind[kind]}`;
    const translated = this.localeService.translate(key);
    return translated === key ? String(kind) : translated;
  }

  getPartyTypeLabel(partyType: LedgerPartyType): string {
    switch (partyType) {
      case LedgerPartyType.Company:
        return this.localeService.translate('orders.partyCompany');
      case LedgerPartyType.Merchant:
        return this.localeService.translate('orders.partyMerchant');
      case LedgerPartyType.Delivery:
        return this.localeService.translate('orders.partyDelivery');
      default:
        return this.localeService.translate('common.noData');
    }
  }

  getPaymentMethodLabel(method: PaymentMethod): string {
    switch (method) {
      case PaymentMethod.Cash:
        return this.localeService.translate('common.cash');
      case PaymentMethod.PayPal:
        return this.localeService.translate('common.paypal');
      default:
        return this.localeService.translate('common.noData');
    }
  }

  getPaymentStateLabel(state: PaymentState): string {
    switch (state) {
      case PaymentState.Pending:
        return this.localeService.translate('common.pending');
      case PaymentState.Paid:
        return this.localeService.translate('common.paid');
      case PaymentState.Failed:
        return this.localeService.translate('common.failed');
      case PaymentState.Refunded:
        return this.localeService.translate('common.refunded');
      default:
        return this.localeService.translate('common.noData');
    }
  }

  getPaymentStateClass(state: PaymentState): string {
    switch (state) {
      case PaymentState.Pending:
        return 'od__pay--pending';
      case PaymentState.Paid:
        return 'od__pay--paid';
      case PaymentState.Failed:
        return 'od__pay--failed';
      case PaymentState.Refunded:
        return 'od__pay--refunded';
      default:
        return '';
    }
  }

  canSendToMerchants(): boolean {
    if (this.isCancelled) return false;
    return this.order?.orderState === OrderState.Pending;
  }

  canConfirm(): boolean {
    if (this.isCancelled) return false;
    return this.order?.orderState === OrderState.MerchantConfirmed;
  }

  canAssignDelivery(): boolean {
    if (this.isCancelled) return false;
    return this.order?.orderState === OrderState.Confirmed;
  }

  canMarkHandover(): boolean {
    if (this.isCancelled) return false;
    const state = this.order?.orderState;
    return (state === OrderState.DeliveryAssigned || state === OrderState.OnWay)
      && this.unreceivedDeliveryVehicles.length > 0;
  }

  canCancel(): boolean {
    if (!this.order || this.isCancelled) return false;
    const state = this.order.orderState;
    if (state !== OrderState.Pending
      && state !== OrderState.MerchantPending
      && state !== OrderState.MerchantConfirmed
      && state !== OrderState.Confirmed
      && state !== OrderState.DeliveryAssigned) {
      return false;
    }
    return !hasAnyReceivedFromOwner(this.order.orderVehicles || []);
  }

  canEdit(): boolean {
    if (!this.order || this.isCancelled) return false;
    if (this.order.orderState !== OrderState.Pending) return false;
    const blocked = this.order.orderPayments?.some(
      p => p.state === PaymentState.Paid || p.state === PaymentState.Refunded
    );
    return !blocked;
  }

  onEdit(): void {
    if (!this.orderId) return;
    this.router.navigate(['/main/orders', this.orderId, 'edit']);
  }

  get isCancelled(): boolean {
    if (!this.order) return false;
    if (this.order.orderState === OrderState.Cancelled) return true;
    return !!this.order.orderCancellationFee
      || !!this.order.refundablePaypalAmount
      || this.order.moneyRefunded === true;
  }

  get isRejectedReceipt(): boolean {
    return this.order?.orderState === OrderState.CustomerRejectedReceipt;
  }

  get showPipeline(): boolean {
    return !this.isCancelled && !this.isRejectedReceipt;
  }

  // ── Invoice / operations tabs ─────────────────────────────────────
  readonly tabs: { key: OrderDetailTab; labelKey: string }[] = [
    { key: 'merchants', labelKey: 'orders.tabMerchants' },
    { key: 'riders', labelKey: 'orders.tabRiders' },
    { key: 'journal', labelKey: 'orders.tabJournal' },
    { key: 'documents', labelKey: 'orders.tabDocuments' }
  ];
  activeTab: OrderDetailTab = 'merchants';

  tabCount(tab: OrderDetailTab): number {
    switch (tab) {
      case 'merchants': return this.order?.merchantOrders?.length || 0;
      case 'riders': return this.order?.deliveryOrderPaymentDetails?.length || 0;
      case 'journal': return this.order?.orderJournals?.length || 0;
      case 'documents': return this.order?.passportImage ? 1 : 0;
    }
  }

  /** Company debit/credit totals on this order's journal. */
  get companyLedger(): { debit: number; credit: number } {
    let debit = 0;
    let credit = 0;
    for (const j of this.order?.orderJournals || []) {
      if (j.partyType !== LedgerPartyType.Company) continue;
      if (j.direction === JournalDirection.Debit) debit += j.amount || 0;
      else credit += j.amount || 0;
    }
    return { debit, credit };
  }

  /** Merchant / rider name for a journal row, resolved from this order's payout snapshots. */
  journalPartyName(j: { partyType: LedgerPartyType; partyId?: number | null }): string {
    if (j.partyType === LedgerPartyType.Company) return 'YallaScoot';
    if (j.partyType === LedgerPartyType.Merchant) {
      return this.order?.merchantOrderPaymentDetails?.find(p => p.merchantId === j.partyId)?.merchantName
        || this.order?.orderVehicles?.find(v => v.merchantId === j.partyId)?.merchantName
        || `#${j.partyId}`;
    }
    return this.order?.deliveryOrderPaymentDetails?.find(p => p.deliveryId === j.partyId)?.deliveryName || `#${j.partyId}`;
  }

  /** Short status for an invoice line, from the vehicle's lifecycle flags. */
  vehicleLine(v: OrderVehicleDto): { labelKey: string; tone: 'ok' | 'info' | 'warn' | 'red' | 'mute' } {
    if (v.deliveryFailed) return { labelKey: 'orders.vehicleCancelled', tone: 'mute' };
    if (v.merchantResponseStatus === MerchantVehicleResponseStatus.Declined) return { labelKey: 'orders.lineDeclined', tone: 'mute' };
    if (v.deliveredToOwner) return { labelKey: 'orders.cycleReturnedToOwner', tone: 'mute' };
    if (v.receivedFromCustomer) return { labelKey: 'orders.cycleReceivedFromCustomer', tone: 'info' };
    if (v.deliveredToCustomer) return { labelKey: 'orders.cycleDeliveredToCustomer', tone: 'ok' };
    if (v.receivedFromOwner) return { labelKey: 'orders.lineWithRider', tone: 'red' };
    if (v.merchantResponseStatus === MerchantVehicleResponseStatus.Pending) return { labelKey: 'orders.lineAwaitingMerchant', tone: 'warn' };
    return { labelKey: 'orders.lineReady', tone: 'info' };
  }

  // ── Order page (Shopify-style record) ─────────────────────────────
  showMoreMenu = false;
  showJournal = false;
  copiedKey: string | null = null;

  /** Total of captured payments. */
  get paidAmount(): number {
    return (this.order?.orderPayments || [])
      .filter(p => p.state === PaymentState.Paid)
      .reduce((sum, p) => sum + (p.total || 0), 0);
  }

  /** Payment status badge shown next to the order number. */
  get paymentBadge(): { labelKey: string; tone: 'ok' | 'warn' | 'red' | 'mute' | 'info' } {
    const payments = this.order?.orderPayments || [];
    if (payments.some(p => p.state === PaymentState.Refunded) || this.order?.moneyRefunded) {
      return { labelKey: 'orders.payRefunded', tone: 'mute' };
    }
    const total = this.order?.orderTotal || 0;
    const paid = this.paidAmount;
    if (paid > 0 && paid >= total) return { labelKey: 'orders.payPaid', tone: 'ok' };
    if (paid > 0) return { labelKey: 'orders.payPartial', tone: 'warn' };
    if (payments.some(p => p.state === PaymentState.Failed)) return { labelKey: 'orders.payFailed', tone: 'red' };
    if (this.order?.paymentMethod === PaymentMethod.Cash) return { labelKey: 'orders.payOnDelivery', tone: 'warn' };
    return { labelKey: 'orders.payPending', tone: 'warn' };
  }

  /** Fulfilment status of one merchant's vehicles (badge on the merchant card). */
  groupTone(vehicles: OrderVehicleDto[]): 'ok' | 'info' | 'warn' | 'red' | 'mute' {
    return this.groupStatus(vehicles).tone;
  }

  groupLabelKey(vehicles: OrderVehicleDto[]): string {
    return this.groupStatus(vehicles).labelKey;
  }

  private groupStatus(vehicles: OrderVehicleDto[]): { labelKey: string; tone: 'ok' | 'info' | 'warn' | 'red' | 'mute' } {
    const live = vehicles.filter(v => !v.deliveryFailed && v.merchantResponseStatus !== MerchantVehicleResponseStatus.Declined);
    if (!live.length) return { labelKey: 'orders.lineDeclined', tone: 'mute' };
    if (live.every(v => v.deliveredToOwner)) return { labelKey: 'orders.cycleReturnedToOwner', tone: 'mute' };
    if (live.every(v => v.deliveredToCustomer)) return { labelKey: 'orders.groupWithCustomer', tone: 'ok' };
    if (live.some(v => v.receivedFromOwner)) return { labelKey: 'orders.lineWithRider', tone: 'red' };
    if (live.some(v => v.merchantResponseStatus === MerchantVehicleResponseStatus.Pending)) {
      return { labelKey: 'orders.lineAwaitingMerchant', tone: 'warn' };
    }
    return { labelKey: 'orders.lineReady', tone: 'info' };
  }

  merchantInviteLabel(merchantId: number): string {
    const invite = this.order?.merchantOrders?.find(m => m.merchantId === merchantId);
    return invite ? this.getMerchantStatusLabel(invite.responseStatus) : '—';
  }

  merchantInviteRejected(merchantId: number): boolean {
    return this.order?.merchantOrders?.find(m => m.merchantId === merchantId)?.responseStatus === MerchantOrderResponseStatus.Rejected;
  }

  get addressText(): string {
    const o = this.order;
    if (!o) return '';
    return [o.hotelName, o.hotelAddress, [o.destinationZoneName, o.cityName].filter(Boolean).join(', '), o.hotelPhone]
      .filter(Boolean)
      .join('\n');
  }

  copy(text: string | null | undefined, key: string): void {
    if (!text) return;
    navigator.clipboard?.writeText(text).then(() => {
      this.copiedKey = key;
      setTimeout(() => {
        if (this.copiedKey === key) this.copiedKey = null;
      }, 1500);
    }).catch(() => undefined);
  }

  /** Everything that happened to the order, newest first. */
  get timeline(): Array<{ date: Date; text: string; detail?: string; tone: 'ok' | 'info' | 'warn' | 'red' | 'mute' }> {
    const o = this.order;
    if (!o) return [];
    const t = (k: string, p?: Record<string, unknown>) => this.localeService.translate(k, p as any);
    const money = (v: number) => new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(v);
    const events: Array<{ date: Date; text: string; detail?: string; tone: 'ok' | 'info' | 'warn' | 'red' | 'mute' }> = [];

    if (o.createdDate) {
      events.push({ date: new Date(o.createdDate), text: t('orders.evCreated', { name: o.customerName }), tone: 'info' });
    }

    for (const mo of o.merchantOrders || []) {
      if (mo.createdDate) {
        events.push({ date: new Date(mo.createdDate), text: t('orders.evSentToMerchant', { name: mo.merchantName }), tone: 'mute' });
      }
      if (mo.respondedAt) {
        events.push({
          date: new Date(mo.respondedAt),
          text: t('orders.evMerchantResponded', { name: mo.merchantName, status: this.getMerchantStatusLabel(mo.responseStatus) }),
          detail: mo.rejectReason || undefined,
          tone: mo.responseStatus === MerchantOrderResponseStatus.Rejected ? 'red' : 'ok'
        });
      }
    }

    // Lifecycle steps, dated by the first photo taken at each step.
    const firstPhoto = new Map<string, Date>();
    for (const img of this.dispatch.handoverImages || []) {
      const key = `${img.vehicleId}:${img.step}`;
      const d = new Date(img.createdDate);
      if (!firstPhoto.has(key) || d < firstPhoto.get(key)!) firstPhoto.set(key, d);
    }
    const stepKeys: Record<number, string> = {
      1: 'orders.cyclePickup',
      2: 'orders.cycleDeliveredToCustomer',
      3: 'orders.cycleReceivedFromCustomer',
      4: 'orders.cycleReturnedToOwner'
    };
    firstPhoto.forEach((date, key) => {
      const [vehicleId, step] = key.split(':').map(Number);
      const v = o.orderVehicles?.find(x => x.vehicleId === vehicleId);
      events.push({ date, text: `${t(stepKeys[step])} · ${v?.vehicleName || ''} #${v?.vehicleCode || vehicleId}`, tone: step === 2 ? 'ok' : 'info' });
    });

    for (const p of o.orderPayments || []) {
      if (!p.createdDate) continue;
      events.push({
        date: new Date(p.createdDate),
        text: t('orders.evPayment', { amount: money(p.total), method: this.getPaymentMethodLabel(p.paymentMethod) }),
        detail: this.getPaymentStateLabel(p.state),
        tone: p.state === PaymentState.Paid ? 'ok' : p.state === PaymentState.Failed ? 'red' : 'warn'
      });
    }

    for (const j of o.orderJournals || []) {
      if (!j.createdDate) continue;
      const sign = j.direction === JournalDirection.Credit ? '+' : '−';
      events.push({
        date: new Date(j.createdDate),
        text: `${this.getJournalKindLabel(j.entryKind)} · ${sign}${money(j.amount)}`,
        detail: `${this.getPartyTypeLabel(j.partyType)} · ${this.journalPartyName(j)}${j.vehicleCode ? ' · #' + j.vehicleCode : ''}`,
        tone: 'mute'
      });
    }

    return events.sort((a, b) => b.date.getTime() - a.date.getTime());
  }

  /** Badge colour for the order state. */
  stateTone(state: OrderState): 'ok' | 'info' | 'warn' | 'red' | 'mute' {
    switch (state) {
      case OrderState.Pending:
      case OrderState.MerchantPending:
        return 'warn';
      case OrderState.MerchantConfirmed:
      case OrderState.Confirmed:
      case OrderState.DeliveryAssigned:
        return 'info';
      case OrderState.OnWay:
      case OrderState.CustomerRejectedReceipt:
        return 'red';
      case OrderState.CustomerReceived:
        return 'ok';
      default:
        return 'mute';
    }
  }

  /** One-line hint for what the admin should do next in the current state. */
  get nextStepKey(): string | null {
    switch (this.order?.orderState) {
      case OrderState.Pending:
        return 'orders.nextSendToMerchants';
      case OrderState.MerchantPending:
        return this.hasDeclinedVehicles ? 'orders.declinedVehiclesBanner' : 'orders.waitingMerchants';
      case OrderState.MerchantConfirmed:
        return 'orders.nextConfirm';
      case OrderState.Confirmed:
        return 'orders.assignPerVehicleHint';
      case OrderState.DeliveryAssigned:
        return 'orders.pickupHint';
      case OrderState.OnWay:
        return 'orders.nextOnWay';
      case OrderState.CustomerReceived:
        return 'orders.returnHint';
      default:
        return null;
    }
  }

  // ── Thermal receipt (80 mm roll) ──────────────────────────────────
  /** Prints a compact POS-style receipt from a hidden frame, independent of the page layout. */
  onPrint(): void {
    if (!this.order) return;
    const frame = document.createElement('iframe');
    frame.setAttribute('aria-hidden', 'true');
    frame.style.cssText = 'position:fixed;width:0;height:0;border:0;right:0;bottom:0;visibility:hidden';
    document.body.appendChild(frame);
    const win = frame.contentWindow;
    const doc = frame.contentDocument;
    if (!win || !doc) {
      frame.remove();
      return;
    }
    doc.open();
    doc.write(this.buildReceiptHtml());
    doc.close();

    let removed = false;
    const cleanup = () => {
      if (removed) return;
      removed = true;
      frame.remove();
    };
    win.onafterprint = () => setTimeout(cleanup, 100);

    // Print once the logo has loaded (or failed) so the copy is complete.
    const logo = doc.querySelector('img') as HTMLImageElement | null;
    const print = () => {
      win.focus();
      win.print();
      setTimeout(cleanup, 60000);
    };
    if (logo && !logo.complete) {
      logo.addEventListener('load', print, { once: true });
      logo.addEventListener('error', print, { once: true });
    } else {
      setTimeout(print, 50);
    }
  }

  private buildReceiptHtml(): string {
    const o = this.order!;
    const tr = (key: string) => this.localeService.translate(key);
    const isAr = this.localeService.locale() === 'ar';
    const locale = isAr ? 'ar-EG' : 'en-GB';
    const entities: Record<string, string> = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' };
    const esc = (v: unknown) => String(v ?? '').replace(/[&<>"']/g, c => entities[c]);
    const money = (v: number | null | undefined) =>
      new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(v ?? 0);
    const date = (v: Date | string | null | undefined, withTime = false) => {
      if (!v) return '—';
      const d = new Date(v);
      if (Number.isNaN(d.getTime())) return '—';
      return new Intl.DateTimeFormat(locale, withTime
        ? { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' }
        : { day: '2-digit', month: 'short', year: 'numeric' }).format(d);
    };
    const row = (label: string, value: string, cls = '') =>
      `<tr class="${cls}"><td>${label}</td><td class="r">${value}</td></tr>`;
    const days = this.reservationDays;
    const totals = o.orderTotals;

    const items = (o.orderVehicles || [])
      .filter(v => !v.deliveryFailed && v.merchantResponseStatus !== MerchantVehicleResponseStatus.Declined)
      .map(v => `
        <div class="item">
          <div class="item-name">${esc(v.vehicleName)} <span class="muted">#${esc(v.vehicleCode)}</span></div>
          <table>
            ${row(`${days} × ${money(v.price)}`, money((v.price || 0) * days))}
            ${row(esc(tr('orders.deliveryFees')), money(v.deliveryFee))}
          </table>
        </div>`).join('');

    const sums = [
      totals ? row(esc(tr('orders.subTotal')), money(totals.subTotal)) : '',
      totals ? row(esc(tr('orders.deliveryFees')), money(totals.deliveryFees)) : '',
      totals ? row(esc(tr('orders.serviceFees')), money(totals.serviceFees)) : '',
      totals && totals.urgentFees > 0 ? row(esc(tr('orders.urgentFees')), money(totals.urgentFees)) : '',
      totals && totals.tieredDiscount ? row(esc(tr('orders.tieredDiscount')), '-' + money(totals.tieredDiscount)) : '',
      o.previousDebt > 0 ? row(esc(tr('orders.previousDebt')), money(o.previousDebt)) : ''
    ].join('');

    const payments = (o.orderPayments || [])
      .map(p => row(`${esc(this.getPaymentMethodLabel(p.paymentMethod))} · ${esc(this.getPaymentStateLabel(p.state))}`, money(p.total)))
      .join('');

    const logoUrl = `${location.origin}/assets/images/Logo.png`;
    const numAlign = isAr ? 'left' : 'right';
    const numPad = isAr ? 'right' : 'left';
    const font = isAr ? 'Tahoma, "Segoe UI", Arial, sans-serif' : '"Segoe UI", Arial, Helvetica, sans-serif';

    return `<!doctype html>
<html lang="${isAr ? 'ar' : 'en'}" dir="${isAr ? 'rtl' : 'ltr'}">
<head>
<meta charset="utf-8">
<title>${esc(o.orderCode)}</title>
<style>
  @page { size: 80mm auto; margin: 0; }
  * { box-sizing: border-box; }
  html, body { margin: 0; padding: 0; background: #fff; color: #000; }
  body { width: 100%; max-width: 80mm; padding: 4mm 4mm 6mm; font: 11px/1.35 ${font}; }
  .c { text-align: center; }
  .logo { display: block; width: 24mm; margin: 0 auto 1.5mm; }
  .brand { font-size: 15px; font-weight: 800; letter-spacing: .3px; }
  .muted { color: #444; font-weight: 400; }
  .small { font-size: 9.5px; }
  hr { border: 0; border-top: 1px dashed #000; margin: 2.5mm 0; }
  hr.solid { border-top: 1.5px solid #000; }
  table { width: 100%; border-collapse: collapse; }
  td { padding: .4mm 0; vertical-align: top; }
  td.r { text-align: ${numAlign}; white-space: nowrap; padding-${numPad}: 2mm; font-variant-numeric: tabular-nums; }
  .title { font-size: 12px; font-weight: 800; letter-spacing: 1px; text-transform: uppercase; margin-bottom: 1mm; }
  .item { margin-bottom: 1.6mm; }
  .item-name { font-weight: 700; }
  .total td { font-size: 14px; font-weight: 800; padding: 1mm 0; }
  .meta td:first-child { color: #333; }
</style>
</head>
<body>
  <div class="c">
    <img class="logo" src="${logoUrl}" alt="">
    <div class="brand">YallaScoot</div>
    <div class="small muted">${esc(tr('orders.receiptCompanyLine'))}</div>
  </div>
  <hr>
  <div class="c title">${esc(tr('orders.rentalReceipt'))}</div>
  <table class="meta">
    ${row(esc(tr('orders.orderNo')), esc(o.orderCode))}
    ${row(esc(tr('orders.issuedOn')), date(o.createdDate, true))}
    ${row(esc(tr('common.status')), esc(this.getStateLabel(this.displayState)))}
  </table>
  <hr>
  <table class="meta">
    ${row(esc(tr('orders.customer')), esc(o.customerName))}
    ${o.customerMobileNumber ? row(esc(tr('common.mobile')), `<span dir="ltr">${esc(o.customerMobileNumber)}</span>`) : ''}
    ${o.hotelName ? row(esc(tr('orders.hotelName')), esc(o.hotelName)) : ''}
    ${row(esc(tr('orders.destinationZone')), esc(o.destinationZoneName || o.cityName))}
    ${row(esc(tr('orders.rentalPeriod')), `${date(o.reservationDateFrom)} – ${date(o.reservationDateTo)}`)}
    ${row(esc(tr('orders.lineDays')), String(days))}
  </table>
  <hr>
  ${items}
  <hr>
  <table>${sums}</table>
  <hr class="solid">
  <table>${row(esc(tr('orders.totalDue')), `${money(o.orderTotal)} ${esc(tr('common.currency'))}`, 'total')}</table>
  <hr class="solid">
  <table class="meta">
    ${row(esc(tr('orders.paymentMethod')), esc(this.getPaymentMethodLabel(o.paymentMethod)))}
    ${payments}
  </table>
  <hr>
  <div class="c small">${esc(tr('orders.invoiceThanks'))}</div>
  <div class="c small muted">${esc(o.orderCode)} · ${date(new Date(), true)}</div>
</body>
</html>`;
  }

  get displayState(): OrderState {
    if (!this.order) return OrderState.Pending;
    return this.isCancelled ? OrderState.Cancelled : this.order.orderState;
  }

  get reservationDays(): number {
    if (!this.order?.reservationDateFrom || !this.order?.reservationDateTo) return 0;
    const from = new Date(this.order.reservationDateFrom);
    const to = new Date(this.order.reservationDateTo);
    if (Number.isNaN(from.getTime()) || Number.isNaN(to.getTime()) || from > to) return 0;
    return Math.max(1, Math.round((to.getTime() - from.getTime()) / 86400000) + 1);
  }

  getVehicleStatusLabel(status: VehicleStatus | number): string {
    switch (status) {
      case VehicleStatus.Available:
        return this.localeService.translate('vehicles.available');
      case VehicleStatus.UnderMaintenance:
        return this.localeService.translate('vehicles.maintenance');
      case VehicleStatus.Rented:
        return this.localeService.translate('vehicles.rented');
      default:
        return this.localeService.translate('common.noData');
    }
  }

  getVehicleStatusBadgeClass(status: VehicleStatus | number): string {
    switch (status) {
      case VehicleStatus.Available:
        return 'od__vehicle-badge--ok';
      case VehicleStatus.UnderMaintenance:
        return 'od__vehicle-badge--warn';
      case VehicleStatus.Rented:
        return 'od__vehicle-badge--rented';
      default:
        return 'od__vehicle-badge--muted';
    }
  }

  get hasPrimaryAction(): boolean {
    return this.canSendToMerchants()
      || this.canConfirm()
      || this.canMarkHandover()
      || this.order?.orderState === OrderState.MerchantPending
      || this.order?.orderState === OrderState.Confirmed
      || this.order?.orderState === OrderState.OnWay
      || this.order?.orderState === OrderState.CustomerReceived
      || this.order?.orderState === OrderState.DeliveryAssigned;
  }

  getStepStatus(stepState: OrderState): 'done' | 'active' | 'upcoming' {
    if (!this.order || this.isCancelled || this.isRejectedReceipt) return 'upcoming';
    if (this.order.orderState > stepState) return 'done';
    if (this.order.orderState === stepState) return 'active';
    return 'upcoming';
  }

  showSuccessMessage(message: string): void {
    this.successMessage = message;
    this.errorMessage = '';
    setTimeout(() => {
      this.successMessage = '';
    }, 5000);
  }

  showErrorMessage(message: string): void {
    this.errorMessage = message;
    this.successMessage = '';
    setTimeout(() => {
      this.errorMessage = '';
    }, 5000);
  }

  isActionLoading(action: string): boolean {
    return this.actionLoading === action;
  }

  get OrderState() {
    return OrderState;
  }

  get MerchantOrderResponseStatus() {
    return MerchantOrderResponseStatus;
  }

  get MerchantVehicleResponseStatus() {
    return MerchantVehicleResponseStatus;
  }

  get JournalDirection() {
    return JournalDirection;
  }

  get PaymentMethod() {
    return PaymentMethod;
  }

  get PaymentState() {
    return PaymentState;
  }

  get FaultParty() {
    return FaultParty;
  }
}

