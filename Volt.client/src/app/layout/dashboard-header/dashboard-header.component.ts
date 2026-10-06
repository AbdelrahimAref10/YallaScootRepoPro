import { Component, EventEmitter, Input, Output, HostListener, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NavigationEnd, Router, RouterModule } from '@angular/router';
import { Subscription, filter } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { AdminNotificationService, AdminNotification } from '../../core/services/admin-notification.service';
import {
  MerchantNotificationService,
  MerchantNotification
} from '../../core/services/merchant-notification.service';
import { NotificationDropdownComponent, Notification } from './notification-dropdown/notification-dropdown.component';
import { ThemeToggleComponent } from '../../shared/components/theme-toggle/theme-toggle.component';
import { LangSwitcherComponent } from '../../shared/components/lang-switcher/lang-switcher.component';
import { TranslatePipe } from '../../shared/pipes/translate.pipe';

export type DashboardNotificationSource = 'admin' | 'merchant';

/** First route segment under /main or /merchant → nav label key for the breadcrumb. */
const SECTION_LABELS: Record<string, string> = {
  dashboard: 'nav.dashboard',
  home: 'merchant.nav.home',
  orders: 'nav.orders',
  shifts: 'nav.shifts',
  merchants: 'nav.merchants',
  deliveries: 'nav.deliveries',
  customers: 'nav.customers',
  vehicles: 'nav.vehicles',
  categories: 'nav.categories',
  subcategories: 'nav.subcategories',
  cities: 'nav.cities',
  settlements: 'nav.settlements',
  journals: 'nav.journals',
  reports: 'nav.reports',
  users: 'nav.systemUsers',
  roles: 'nav.roles',
  support: 'nav.support',
  profile: 'nav.profile',
  payments: 'merchant.nav.payments'
};

@Component({
  selector: 'app-dashboard-header',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    NotificationDropdownComponent,
    ThemeToggleComponent,
    LangSwitcherComponent,
    TranslatePipe
  ],
  templateUrl: './dashboard-header.component.html',
  styleUrl: './dashboard-header.component.css'
})
export class DashboardHeaderComponent implements OnInit, OnDestroy {
  @Input() isMobileNavOpen = false;
  @Input() showNotifications = true;
  @Input() notificationSource: DashboardNotificationSource = 'admin';
  @Input() profileRoute = '/main/profile';
  @Input() ordersBaseRoute = '/main/orders';
  @Output() toggleSidebar = new EventEmitter<void>();

  isUserMenuOpen = false;
  isNotificationOpen = false;
  userData: any;

  notifications: Notification[] = [];
  unreadCount = 0;
  /** Breadcrumb: section label key and an optional detail step (new / edit / record). */
  sectionKey: string | null = null;
  detailKey: string | null = null;
  readonly today = new Date();
  private subscriptions = new Subscription();

  constructor(
    private router: Router,
    private authService: AuthService,
    private adminNotificationService: AdminNotificationService,
    private merchantNotificationService: MerchantNotificationService
  ) {
    this.userData = this.authService.getUserData();
  }

  get avatarInitial(): string {
    const name = this.userData?.userName || 'A';
    return String(name).charAt(0).toUpperCase();
  }

  private get isMerchantMode(): boolean {
    return this.notificationSource === 'merchant';
  }

  get consoleKey(): string {
    return this.isMerchantMode ? 'header.merchantConsole' : 'header.adminConsole';
  }

  private updateBreadcrumb(url: string): void {
    const parts = url.split('?')[0].split('#')[0].split('/').filter(Boolean);
    const section = parts[1];
    this.sectionKey = section ? SECTION_LABELS[section] ?? null : null;
    const rest = parts.slice(2);
    if (!rest.length) {
      this.detailKey = null;
    } else if (rest.includes('new')) {
      this.detailKey = 'header.crumbNew';
    } else if (rest.includes('edit')) {
      this.detailKey = 'header.crumbEdit';
    } else {
      this.detailKey = 'header.crumbDetails';
    }
  }

  ngOnInit(): void {
    this.updateBreadcrumb(this.router.url);
    this.subscriptions.add(
      this.router.events
        .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
        .subscribe(e => this.updateBreadcrumb(e.urlAfterRedirects))
    );

    if (!this.showNotifications) {
      return;
    }

    this.loadNotifications();
    this.loadUnreadCount();

    if (this.isMerchantMode) {
      this.subscriptions.add(
        this.merchantNotificationService.notifications$.subscribe(notifications => {
          this.notifications = notifications.map(n => this.mapMerchantNotification(n));
        })
      );
      this.subscriptions.add(
        this.merchantNotificationService.unreadCount$.subscribe(count => {
          this.unreadCount = count;
        })
      );
      return;
    }

    this.subscriptions.add(
      this.adminNotificationService.notifications$.subscribe(notifications => {
        this.notifications = notifications.map(n => this.mapAdminNotification(n));
      })
    );
    this.subscriptions.add(
      this.adminNotificationService.unreadCount$.subscribe(count => {
        this.unreadCount = count;
      })
    );
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }

  loadNotifications(): void {
    if (this.isMerchantMode) {
      this.merchantNotificationService.loadNotifications(undefined, 0, 50).subscribe();
      return;
    }
    this.adminNotificationService.loadNotifications(false, 0, 50).subscribe();
  }

  loadUnreadCount(): void {
    if (this.isMerchantMode) {
      this.merchantNotificationService.updateUnreadCount();
      return;
    }
    this.adminNotificationService.updateUnreadCount();
  }

  mapAdminNotification(adminNotification: AdminNotification): Notification {
    return {
      id: adminNotification.adminNotificationId.toString(),
      type: this.getNotificationType(adminNotification.notificationType),
      title: adminNotification.title,
      message: adminNotification.message,
      timestamp: adminNotification.createdDate,
      isRead: adminNotification.isRead,
      actionUrl: adminNotification.orderId ? `${this.ordersBaseRoute}/${adminNotification.orderId}` : undefined
    };
  }

  mapMerchantNotification(notification: MerchantNotification): Notification {
    return {
      id: notification.merchantNotificationId.toString(),
      type: this.getNotificationType(notification.notificationType),
      title: notification.title,
      message: notification.message,
      timestamp: notification.createdDate,
      isRead: notification.isRead,
      actionUrl: notification.orderId ? `${this.ordersBaseRoute}/${notification.orderId}` : undefined
    };
  }

  getNotificationType(notificationType: number): 'system' | 'user' | 'order' | 'warning' | 'info' {
    switch (notificationType) {
      case 1:
      case 2:
      case 3:
      case 4:
      case 5:
      case 6:
      case 7:
        return 'order';
      default:
        return 'info';
    }
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: Event): void {
    const target = event.target as HTMLElement;
    if (!target.closest('.dashboard-header__notification-menu') &&
        !target.closest('.notification-dropdown')) {
      this.isNotificationOpen = false;
    }
    if (!target.closest('.dashboard-header__user-menu') &&
        !target.closest('.dashboard-header__dropdown')) {
      this.isUserMenuOpen = false;
    }
  }

  onToggleSidebar(): void {
    this.toggleSidebar.emit();
  }

  onToggleUserMenu(event: Event): void {
    event.stopPropagation();
    this.isUserMenuOpen = !this.isUserMenuOpen;
    this.isNotificationOpen = false;
  }

  onToggleNotification(event: Event): void {
    event.stopPropagation();
    this.isNotificationOpen = !this.isNotificationOpen;
    this.isUserMenuOpen = false;
    if (this.isNotificationOpen) {
      this.loadNotifications();
      this.loadUnreadCount();
    }
  }

  onMarkAsRead(notificationId: string): void {
    const id = parseInt(notificationId, 10);
    const request$ = this.isMerchantMode
      ? this.merchantNotificationService.markAsRead(id)
      : this.adminNotificationService.markAsRead(id);

    request$.subscribe({
      next: () => undefined,
      error: (error) => console.error('Error marking notification as read:', error)
    });
  }

  onMarkAllAsRead(): void {
    const request$ = this.isMerchantMode
      ? this.merchantNotificationService.markAllAsRead()
      : this.adminNotificationService.markAllAsRead();

    request$.subscribe({
      next: () => undefined,
      error: (error) => console.error('Error marking all notifications as read:', error)
    });
  }

  onNotificationClick(notification: Notification): void {
    if (!notification.isRead) {
      this.onMarkAsRead(notification.id);
    }
    this.isNotificationOpen = false;
  }

  onLogout(): void {
    this.authService.logout();
  }
}
