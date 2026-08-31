import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { PortfolioSummary, Subscription } from '../../core/api.models';
import { AuthService } from '../../core/auth.service';
import { readableHttpError } from '../../core/http-error';
import { PortalApiService } from '../../core/portal-api.service';

@Component({
  selector: 'app-dashboard-page',
  imports: [CurrencyPipe, DatePipe, DecimalPipe, RouterLink],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class DashboardPage implements OnInit {
  private readonly api = inject(PortalApiService);
  protected readonly auth = inject(AuthService);
  protected readonly subscription = signal<Subscription | null>(null);
  protected readonly portfolios = signal<PortfolioSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly portfolioLoading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly portfolioError = signal<string | null>(null);
  protected readonly totalValue = computed(() =>
    this.portfolios().reduce((sum, item) => sum + item.marketValue, 0),
  );
  protected readonly totalDayChange = computed(() =>
    this.portfolios().reduce((sum, item) => sum + item.dayChange, 0),
  );
  protected readonly positionCount = computed(() =>
    this.portfolios().reduce((sum, item) => sum + item.positionCount, 0),
  );

  ngOnInit(): void {
    this.api
      .getSubscription()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (subscription) => this.subscription.set(subscription),
        error: (error: unknown) =>
          this.error.set(readableHttpError(error, 'Subscription information is unavailable.')),
      });

    this.api
      .getPortfolios()
      .pipe(finalize(() => this.portfolioLoading.set(false)))
      .subscribe({
        next: (portfolios) => this.portfolios.set(portfolios),
        error: (error: unknown) =>
          this.portfolioError.set(
            readableHttpError(error, 'Portfolio information is unavailable.'),
          ),
      });
  }
}
