import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { TranslatePipe } from '../../shared/pipes/translate.pipe';
import { AuthService } from '../../core/services/auth.service';
import { ADMIN_MENU_ITEMS, DashboardMenuItem } from '../../core/models/panel-menus';

export type { DashboardMenuItem } from '../../core/models/panel-menus';

@Component({
  selector: 'app-dashboard-sidebar',
  standalone: true,
  imports: [CommonModule, RouterModule, TranslatePipe],
  templateUrl: './dashboard-sidebar.component.html',
  styleUrl: './dashboard-sidebar.component.css'
})
export class DashboardSidebarComponent {
  @Input() isOpen = true;
  @Input() isMobileOpen = false;
  @Input() menuItems: DashboardMenuItem[] = ADMIN_MENU_ITEMS;
  /** Route that should use exact routerLinkActive matching (home/dashboard). */
  @Input() exactActiveRoute = '/main/dashboard';
  @Input() footerKey = 'app.adminFooter';
  @Output() closeMobile: EventEmitter<void> = new EventEmitter<void>();

  constructor(private authService: AuthService) {}

  /** Menu items the user's sub-role may open (reads the permissions signal, so it updates live). */
  get visibleItems(): DashboardMenuItem[] {
    return this.menuItems.filter(item => !item.permission || this.authService.hasPermission(item.permission));
  }

  /** True when this item opens a new section (its group differs from the previous visible item's). */
  startsGroup(index: number): boolean {
    const items = this.visibleItems;
    const key = items[index]?.groupKey;
    return !!key && key !== items[index - 1]?.groupKey;
  }

  onCloseMobile(): void {
    this.closeMobile.emit();
  }

  onNavClick(): void {
    if (this.isMobileOpen) {
      this.closeMobile.emit();
    }
  }
}
