import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MerchantDashboardSummaryDto, MerchantProfileClient } from '../../core/services/clientAPI';
import { LocaleService } from '../../core/services/locale.service';
import { TranslatePipe } from '../../shared/pipes/translate.pipe';

@Component({
  selector: 'app-merchant-home',
  standalone: true,
  imports: [CommonModule, RouterModule, TranslatePipe],
  templateUrl: './merchant-home.component.html',
  styleUrls: [
    '../dashboard/dashboard.component.css',
    './merchant-home.component.css'
  ]
})
export class MerchantHomeComponent implements OnInit {
  private readonly localeService = inject(LocaleService);
  private readonly merchantProfileClient = inject(MerchantProfileClient);

  readonly today = new Date();
  summary: MerchantDashboardSummaryDto | null = null;
  isLoading = false;
  errorMessage = '';

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.isLoading = true;
    this.errorMessage = '';
    this.merchantProfileClient.getDashboard().subscribe({
      next: (data) => {
        this.summary = data;
        this.isLoading = false;
      },
      error: (error: any) => {
        this.isLoading = false;
        this.errorMessage =
          error?.errorMessage ||
          error?.error?.errorMessage ||
          this.localeService.translate('merchant.loadFailed');
      }
    });
  }

  formatAmount(value: number | undefined): string {
    return new Intl.NumberFormat(this.localeService.locale() === 'ar' ? 'ar-EG' : 'en-EG', {
      maximumFractionDigits: 2
    }).format(Number(value || 0));
  }

  formatPercent(value: number | undefined): string {
    const n = Number(value || 0);
    return n % 1 === 0 ? String(n) : n.toFixed(2).replace(/0$/, '');
  }

  formatMoney(value: number | undefined): string {
    return `${Number(value || 0).toFixed(2)} ${this.localeService.translate('common.currency')}`;
  }
}
