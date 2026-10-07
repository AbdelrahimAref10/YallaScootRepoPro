import { Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AccessClient,
  AccessResponse,
  AuthClient,
  AuthResponse,
  LoginCommand,
  RefreshTokenCommand,
  RefreshTokenResponse
} from './clientAPI';
import { Observable, tap, catchError, throwError, BehaviorSubject, map, filter, take, of } from 'rxjs';
import { SignalRService } from './signalr.service';
import { AppRole, AppRoleNames, appRoleFromName } from '../models/app-role';

export interface AuthUserData {
  userId: number;
  userName: string;
  roles: string[];
  role: AppRole | null;
  employeeId?: number | null;
  customerId?: number | null;
  merchantId?: number | null;
  deliveryId?: number | null;
  /** Admin / merchant panel sub-role. */
  subRoleId?: number | null;
  subRoleName?: string | null;
  subRoleNameAr?: string | null;
  /** True for the merchant account owner. */
  isOwner?: boolean;
  /** Permission names, e.g. "Admin.Orders.View" (see core/models/permissions.ts). */
  permissions?: string[];
}

/** Minimum time between two background refreshes of the permissions. */
const ACCESS_REFRESH_INTERVAL_MS = 30_000;

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly TOKEN_KEY = 'auth_token';
  private readonly REFRESH_TOKEN_KEY = 'refresh_token';
  private readonly USER_KEY = 'user_data';
  private refreshTokenInProgress = false;
  private refreshTokenSubject = new BehaviorSubject<string | null>(null);
  private lastAccessLoad = 0;

  /** Current user's permissions; menus and guards read this so they update when it changes. */
  private readonly permissionSet = signal<ReadonlySet<string>>(new Set(this.getUserData()?.permissions ?? []));
  readonly permissions = this.permissionSet.asReadonly();
  private readonly subRole = signal<{ name: string | null; nameAr: string | null }>({
    name: this.getUserData()?.subRoleName ?? null,
    nameAr: this.getUserData()?.subRoleNameAr ?? null
  });
  readonly subRoleInfo = this.subRole.asReadonly();

  constructor(
    private authClient: AuthClient,
    private accessClient: AccessClient,
    private router: Router,
    private signalRService: SignalRService
  ) {
    // Pick up permission changes made by an admin when the user comes back to the tab.
    window.addEventListener('focus', () => this.refreshAccess());
  }

  login(credentials: LoginCommand): Observable<AuthResponse> {
    return this.authClient.login(credentials).pipe(
      tap((response: AuthResponse) => {
        this.persistAuthResponse(response);
        if (response.token && this.isSuperAdmin()) {
          this.signalRService.StartNotificationConnection(response.token);
        }
        if (response.token && this.isMerchant()) {
          this.signalRService.StartMerchantNotificationConnection(response.token);
        }
      }),
      catchError((error) => {
        console.error('Login error:', error);
        return throwError(() => error);
      })
    );
  }

  /** Home route for web login: admin or merchant only. */
  getHomeRouteForCurrentUser(): string | null {
    const role = this.getRole();
    if (role === AppRole.SuperAdmin) {
      return '/main';
    }
    if (role === AppRole.Merchant) {
      return '/merchant';
    }
    return null;
  }

  isSuperAdmin(): boolean {
    return this.getRole() === AppRole.SuperAdmin || this.hasRole(AppRoleNames.SuperAdmin);
  }

  isMerchant(): boolean {
    return this.getRole() === AppRole.Merchant || this.hasRole(AppRoleNames.Merchant);
  }

  /** True when the signed-in user may use this web panel (admin or merchant). */
  canAccessWebPanel(): boolean {
    return this.isSuperAdmin() || this.isMerchant();
  }

  /** Clears tokens/session without navigation (e.g. access denied after login). */
  clearSession(): void {
    this.signalRService.StopNotificationConnection();
    this.clearAuthData();
  }

  logout(): void {
    this.clearSession();
    this.router.navigate(['/login']);
  }

  isAuthenticated(): boolean {
    return !!this.getToken();
  }

  getToken(): string | null {
    return localStorage.getItem(this.TOKEN_KEY);
  }

  getRefreshToken(): string | null {
    return localStorage.getItem(this.REFRESH_TOKEN_KEY);
  }

  getUserData(): AuthUserData | null {
    const userData = localStorage.getItem(this.USER_KEY);
    return userData ? JSON.parse(userData) as AuthUserData : null;
  }

  getRole(): AppRole | null {
    return this.getUserData()?.role ?? null;
  }

  hasRole(roleName: string): boolean {
    const roles = this.getUserData()?.roles ?? [];
    return roles.includes(roleName);
  }

  hasPermission(permission: string): boolean {
    return this.permissionSet().has(permission);
  }

  hasAnyPermission(permissions: readonly string[]): boolean {
    const granted = this.permissionSet();
    return permissions.some(p => granted.has(p));
  }

  /**
   * Reloads sub-role and permissions from the API. Called when a panel opens, after a token
   * refresh, on a 403 and when the tab regains focus. Emits false when the user lost access.
   */
  loadAccess(): Observable<boolean> {
    if (!this.isAuthenticated() || !this.canAccessWebPanel()) {
      return of(false);
    }

    this.lastAccessLoad = Date.now();
    return this.accessClient.getMyAccess().pipe(
      map((access: AccessResponse) => {
        this.applyAccess(access);
        return true;
      }),
      catchError((error) => {
        // 400 = account / sub-role no longer active.
        if (error?.status === 400) {
          this.logout();
          return of(false);
        }
        return of(true);
      })
    );
  }

  /** Fire-and-forget {@link loadAccess}, throttled so focus / 403 bursts make one call. */
  refreshAccess(force = false): void {
    if (!force && Date.now() - this.lastAccessLoad < ACCESS_REFRESH_INTERVAL_MS) {
      return;
    }
    this.loadAccess().subscribe();
  }

  private applyAccess(access: AccessResponse): void {
    const userData = this.getUserData();
    if (!userData) {
      return;
    }
    this.setUserData({
      ...userData,
      subRoleId: access.subRoleId,
      subRoleName: access.subRoleName,
      subRoleNameAr: access.subRoleNameAr,
      isOwner: access.isOwner,
      merchantId: access.merchantId ?? userData.merchantId,
      permissions: access.permissions ?? []
    });
  }

  private persistAuthResponse(response: AuthResponse): void {
    this.setToken(response.token);
    this.setRefreshToken(response.refreshToken);

    const roles = response.roles ?? [];
    const role =
      typeof response.role === 'number' && response.role > 0
        ? (response.role as AppRole)
        : appRoleFromName(roles[0]);

    this.setUserData({
      userId: response.userId,
      userName: response.userName,
      roles,
      role,
      employeeId: response.employeeId,
      customerId: response.customerId,
      merchantId: response.merchantId,
      deliveryId: response.deliveryId,
      subRoleId: response.subRoleId,
      subRoleName: response.subRoleName,
      subRoleNameAr: response.subRoleNameAr,
      isOwner: response.isOwner,
      permissions: response.permissions ?? []
    });
    this.lastAccessLoad = Date.now();
  }

  private setToken(token: string): void {
    localStorage.setItem(this.TOKEN_KEY, token);
  }

  private setRefreshToken(refreshToken: string): void {
    localStorage.setItem(this.REFRESH_TOKEN_KEY, refreshToken);
  }

  private setUserData(userData: AuthUserData): void {
    localStorage.setItem(this.USER_KEY, JSON.stringify(userData));
    this.permissionSet.set(new Set(userData.permissions ?? []));
    this.subRole.set({ name: userData.subRoleName ?? null, nameAr: userData.subRoleNameAr ?? null });
  }

  private clearAuthData(): void {
    localStorage.removeItem(this.TOKEN_KEY);
    localStorage.removeItem(this.REFRESH_TOKEN_KEY);
    localStorage.removeItem(this.USER_KEY);
    this.permissionSet.set(new Set());
    this.subRole.set({ name: null, nameAr: null });
  }

  refreshToken(): Observable<string> {
    const refreshTokenValue = this.getRefreshToken();

    if (!refreshTokenValue) {
      this.logout();
      return throwError(() => new Error('No refresh token available'));
    }

    if (this.refreshTokenInProgress) {
      return this.refreshTokenSubject.asObservable().pipe(
        filter(token => token !== null),
        take(1),
        map(token => token as string)
      );
    }

    this.refreshTokenInProgress = true;
    this.refreshTokenSubject.next(null);

    const command = new RefreshTokenCommand();
    command.refreshToken = refreshTokenValue;

    return this.authClient.refreshToken(command).pipe(
      map((response: RefreshTokenResponse) => {
        this.setToken(response.token);
        this.setRefreshToken(response.refreshToken);
        this.refreshTokenInProgress = false;
        this.refreshTokenSubject.next(response.token);
        this.refreshAccess(true);
        return response.token;
      }),
      catchError((error) => {
        this.refreshTokenInProgress = false;
        this.refreshTokenSubject.next(null);
        this.logout();
        return throwError(() => error);
      })
    );
  }
}
