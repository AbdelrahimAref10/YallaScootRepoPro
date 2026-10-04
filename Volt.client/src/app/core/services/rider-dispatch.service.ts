import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import {
  API_BASE_URL,
  AddCityCommand,
  CityDto,
  DeliveryDto,
  OrderDetailDto,
  PagedResultOfCityDto,
  UpdateCityCommand
} from './clientAPI';

/**
 * Hand-written client for the rider-dispatch endpoints (shifts, per-leg assignment, handover photos,
 * city leg commission). The generated NSwag client does not know these yet and its DTOs drop unknown
 * JSON fields, so everything new is read from the raw response here. Requests go through the same
 * HttpClient, so the auth / refresh interceptors apply as usual.
 */

/** Each vehicle has two trips. Sent and returned as ints. */
export enum DeliveryLeg {
  Delivery = 1,
  Return = 2
}

export enum HandoverStep {
  ReceivedFromOwner = 1,
  DeliveredToCustomer = 2,
  ReceivedFromCustomer = 3,
  DeliveredToOwner = 4
}

export enum HandoverImagePosition {
  Front = 1,
  Back = 2,
  Left = 3,
  Right = 4
}

export enum RiderAvailabilityStatus {
  /** In a running shift and switched online. */
  Available = 1,
  /** In a running shift but offline. */
  InShiftOffline = 2,
  /** No running shift right now. */
  OffShift = 3
}

/** Sunday = 1, Monday = 2, … Saturday = 64. */
export const SHIFT_ALL_DAYS = 127;

export interface ShiftRider {
  deliveryId: number;
  fullName: string;
  mobileNumber: string;
  isOnline: boolean;
}

export interface Shift {
  shiftId: number;
  cityId: number;
  cityName: string;
  name: string;
  /** Egypt local time, "HH:mm". */
  startTime: string;
  /** Egypt local time, "HH:mm". Earlier than (or equal to) startTime means it ends the next day. */
  endTime: string;
  daysOfWeekMask: number;
  isActive: boolean;
  isRunningNow: boolean;
  riders: ShiftRider[];
}

export interface SaveShiftPayload {
  cityId: number;
  name: string;
  startTime: string;
  endTime: string;
  daysOfWeekMask: number;
  isActive: boolean;
  deliveryIds: number[];
}

export interface RiderCandidate {
  deliveryId: number;
  fullName: string;
  mobileNumber: string;
  zoneId: number;
  zoneName: string;
  status: RiderAvailabilityStatus;
  isOnline: boolean;
  isInShift: boolean;
  currentShiftName: string | null;
  currentShiftEndsAt: string | null;
  activeLegsCount: number;
  /** Cash collected from customers and not remitted yet. */
  cashDebt: number;
  /** Null = no limit. */
  cashDebtLimit: number | null;
  /** At or above the limit: the backend refuses him the delivery trip of a cash order. */
  isOverCashDebtLimit: boolean;
}

/** A rider's cash debt and its limit, from the admin delivery list / detail. */
export interface RiderCashDebtInfo {
  cashDebt: number;
  /** Null = no limit. */
  cashDebtLimit: number | null;
}

/** Same rule as the backend `RiderCashDebt.IsOverLimit`. */
export function isOverCashDebtLimit(debt: number, limit: number | null | undefined): boolean {
  return limit != null && debt >= limit;
}

/** AssignDelivery fails with "Cash debt limit reached, …: Name (350 / 300), …". */
export const CASH_DEBT_LIMIT_ERROR_PREFIX = 'Cash debt limit reached';

export interface LegAssignmentItem {
  vehicleId: number;
  deliveryId: number;
  leg: DeliveryLeg;
}

/** `deliveryMenOrders[]` row of the admin order detail, with its leg. */
export interface OrderLegRider {
  deliveryMenOrderId: number;
  orderId: number;
  vehicleId: number;
  vehicleCode: string;
  deliveryId: number;
  deliveryName: string;
  deliveryReceivedFromMerchant: boolean;
  receivedFromMerchantAt: string | null;
  leg: DeliveryLeg;
}

export interface HandoverImage {
  vehicleId: number;
  step: HandoverStep;
  position: HandoverImagePosition;
  imageUrl: string;
  deliveryId: number | null;
  createdDate: string;
}

export interface DeliveryPayoutLeg {
  deliveryOrderPaymentDetailId: number;
  leg: DeliveryLeg;
  commissionPercent: number | null;
}

/** The parts of the admin order detail the generated `OrderDetailDto` does not carry yet. */
export interface OrderDispatchInfo {
  legs: OrderLegRider[];
  handoverImages: HandoverImage[];
  payoutLegs: DeliveryPayoutLeg[];
}

export interface CityCommission {
  deliveryLegCommissionPercent: number;
  returnLegCommissionPercent: number;
}

export const DEFAULT_CITY_COMMISSION: CityCommission = {
  deliveryLegCommissionPercent: 20,
  returnLegCommissionPercent: 20
};

/** Old rows have no leg; they are the delivery trip. Accepts ints or enum names. */
export function normalizeLeg(value: unknown): DeliveryLeg {
  return value === DeliveryLeg.Return || value === 'Return' || value === '2'
    ? DeliveryLeg.Return
    : DeliveryLeg.Delivery;
}

function toNumber(value: unknown, fallback: number): number {
  const n = typeof value === 'string' ? Number(value) : (value as number);
  return typeof n === 'number' && Number.isFinite(n) ? n : fallback;
}

function readCashDebt(raw: any): RiderCashDebtInfo {
  return {
    cashDebt: toNumber(raw?.cashDebt, 0),
    cashDebtLimit: raw?.cashDebtLimit != null ? toNumber(raw.cashDebtLimit, 0) : null
  };
}

function readCommission(raw: any): CityCommission {
  return {
    deliveryLegCommissionPercent: toNumber(raw?.deliveryLegCommissionPercent, DEFAULT_CITY_COMMISSION.deliveryLegCommissionPercent),
    returnLegCommissionPercent: toNumber(raw?.returnLegCommissionPercent, DEFAULT_CITY_COMMISSION.returnLegCommissionPercent)
  };
}

@Injectable({ providedIn: 'root' })
export class RiderDispatchService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL, { optional: true }) ?? '';

  // ── Shifts ───────────────────────────────────────────────────────────
  getShifts(cityId?: number | null): Observable<Shift[]> {
    let params = new HttpParams();
    if (cityId != null) params = params.set('cityId', String(cityId));
    return this.http
      .get<Shift[]>(`${this.baseUrl}/api/admin/Shift`, { params })
      .pipe(map(list => list || []));
  }

  getShift(shiftId: number): Observable<Shift> {
    return this.http.get<Shift>(`${this.baseUrl}/api/admin/Shift/${shiftId}`);
  }

  createShift(payload: SaveShiftPayload): Observable<number> {
    return this.http.post<number>(`${this.baseUrl}/api/admin/Shift`, payload);
  }

  /** Replaces the shift's rider list with `payload.deliveryIds`. */
  updateShift(shiftId: number, payload: SaveShiftPayload): Observable<number> {
    return this.http.put<number>(`${this.baseUrl}/api/admin/Shift/${shiftId}`, payload);
  }

  deleteShift(shiftId: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.baseUrl}/api/admin/Shift/${shiftId}`);
  }

  // ── Riders / assignment ─────────────────────────────────────────────
  /** Riders of the order's city, best first (online in shift → in shift offline → off shift). */
  getCandidatesForOrder(orderId: number): Observable<RiderCandidate[]> {
    return this.getCandidates(new HttpParams().set('orderId', String(orderId)));
  }

  getCandidatesForCity(cityId: number): Observable<RiderCandidate[]> {
    return this.getCandidates(new HttpParams().set('cityId', String(cityId)));
  }

  /**
   * Same query as `AdminDeliveryClient.getAll`, keeping each rider's cash debt and limit
   * (the generated `DeliveryDto` drops them).
   */
  getDeliveries(
    search?: string | null,
    isDeleted?: boolean | null
  ): Observable<{ list: DeliveryDto[]; debts: Map<number, RiderCashDebtInfo> }> {
    let params = new HttpParams();
    if (search) params = params.set('Search', search);
    if (isDeleted != null) params = params.set('IsDeleted', String(isDeleted));
    return this.http.get<any[]>(`${this.baseUrl}/api/admin/AdminDelivery`, { params }).pipe(
      map(raw => {
        const debts = new Map<number, RiderCashDebtInfo>();
        const list = (raw || []).map(item => {
          debts.set(item.deliveryId, readCashDebt(item));
          return DeliveryDto.fromJS(item);
        });
        return { list, debts };
      })
    );
  }

  /** Max collected cash the rider may hold before he must remit. Null removes the limit. */
  setCashDebtLimit(deliveryId: number, cashDebtLimit: number | null): Observable<boolean> {
    return this.http.put<boolean>(`${this.baseUrl}/api/admin/AdminDelivery/${deliveryId}/CashDebtLimit`, {
      deliveryId,
      cashDebtLimit
    });
  }

  /** Assign or reassign riders per (vehicle, leg). */
  assignLegs(orderId: number, assignments: LegAssignmentItem[]): Observable<boolean> {
    return this.http.post<boolean>(`${this.baseUrl}/api/admin/AdminOrder/${orderId}/AssignDelivery`, {
      orderId,
      assignments
    });
  }

  /** Admin order detail as the generated DTO, plus the per-leg riders and handover photos. */
  getOrderDetail(orderId: number): Observable<{ order: OrderDetailDto; dispatch: OrderDispatchInfo }> {
    return this.http.get<any>(`${this.baseUrl}/api/admin/AdminOrder/${orderId}`).pipe(
      map(raw => ({
        order: OrderDetailDto.fromJS(raw),
        dispatch: {
          legs: ((raw?.deliveryMenOrders as any[]) || []).map(d => ({
            deliveryMenOrderId: d.deliveryMenOrderId,
            orderId: d.orderId,
            vehicleId: d.vehicleId,
            vehicleCode: d.vehicleCode,
            deliveryId: d.deliveryId,
            deliveryName: d.deliveryName,
            deliveryReceivedFromMerchant: !!d.deliveryReceivedFromMerchant,
            receivedFromMerchantAt: d.receivedFromMerchantAt ?? null,
            leg: normalizeLeg(d.leg)
          })),
          handoverImages: ((raw?.handoverImages as any[]) || []).map(i => ({
            vehicleId: i.vehicleId,
            step: toNumber(i.step, 0) as HandoverStep,
            position: toNumber(i.position, 0) as HandoverImagePosition,
            imageUrl: i.imageUrl,
            deliveryId: i.deliveryId ?? null,
            createdDate: i.createdDate
          })),
          payoutLegs: ((raw?.deliveryOrderPaymentDetails as any[]) || []).map(p => ({
            deliveryOrderPaymentDetailId: p.deliveryOrderPaymentDetailId,
            leg: normalizeLeg(p.leg),
            commissionPercent: p.commissionPercent != null ? toNumber(p.commissionPercent, 0) : null
          }))
        }
      }))
    );
  }

  // ── City leg commission ─────────────────────────────────────────────
  getCity(cityId: number): Observable<{ city: CityDto; commission: CityCommission }> {
    return this.http.get<any>(`${this.baseUrl}/api/admin/City/${cityId}`).pipe(
      map(raw => ({ city: CityDto.fromJS(raw), commission: readCommission(raw) }))
    );
  }

  /** Same query as `CityClient.getAll`, keeping each city's leg commission. */
  getCities(
    pageNumber: number,
    pageSize: number,
    searchTerm?: string | null,
    isActive?: boolean | null
  ): Observable<{ page: PagedResultOfCityDto; commissions: Map<number, CityCommission> }> {
    let params = new HttpParams()
      .set('PageNumber', String(pageNumber))
      .set('PageSize', String(pageSize));
    if (searchTerm) params = params.set('SearchTerm', searchTerm);
    if (isActive != null) params = params.set('IsActive', String(isActive));
    return this.http.get<any>(`${this.baseUrl}/api/admin/City`, { params }).pipe(
      map(raw => {
        const commissions = new Map<number, CityCommission>();
        for (const item of (raw?.items as any[]) || []) {
          commissions.set(item.cityId, readCommission(item));
        }
        return { page: PagedResultOfCityDto.fromJS(raw), commissions };
      })
    );
  }

  addCity(command: AddCityCommand, commission: CityCommission): Observable<number> {
    return this.http.post<number>(`${this.baseUrl}/api/admin/City`, {
      ...command.toJSON(),
      ...commission
    });
  }

  updateCity(command: UpdateCityCommand, commission: CityCommission): Observable<number> {
    return this.http.put<number>(`${this.baseUrl}/api/admin/City`, {
      ...command.toJSON(),
      ...commission
    });
  }

  private getCandidates(params: HttpParams): Observable<RiderCandidate[]> {
    return this.http
      .get<any[]>(`${this.baseUrl}/api/admin/AdminDelivery/ForAssign`, { params })
      .pipe(
        map(list =>
          (list || []).map(c => ({
            ...c,
            deliveryId: toNumber(c.deliveryId, 0),
            status: toNumber(c.status, RiderAvailabilityStatus.OffShift) as RiderAvailabilityStatus,
            activeLegsCount: toNumber(c.activeLegsCount, 0),
            ...readCashDebt(c),
            isOverCashDebtLimit: c.isOverCashDebtLimit != null
              ? !!c.isOverCashDebtLimit
              : isOverCashDebtLimit(toNumber(c.cashDebt, 0), c.cashDebtLimit != null ? toNumber(c.cashDebtLimit, 0) : null)
          }))
        )
      );
  }
}
