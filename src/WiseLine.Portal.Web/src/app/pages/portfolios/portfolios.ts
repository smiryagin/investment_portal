import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { PortfolioSummary } from '../../core/api.models';
import { readableHttpError } from '../../core/http-error';
import { PortalApiService } from '../../core/portal-api.service';

@Component({
  selector: 'app-portfolios-page',
  imports: [CurrencyPipe, DatePipe, RouterLink],
  templateUrl: './portfolios.html',
  styleUrl: './portfolios.scss',
})
export class PortfoliosPage implements OnInit {
  private readonly api = inject(PortalApiService);
  protected readonly portfolios = signal<PortfolioSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly totalValue = computed(() =>
    this.portfolios().reduce((sum, item) => sum + item.marketValue, 0),
  );

  ngOnInit(): void {
    this.api
      .getPortfolios()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (portfolios) => this.portfolios.set(portfolios),
        error: (error: unknown) =>
          this.error.set(readableHttpError(error, 'Portfolio information is unavailable.')),
      });
  }
}
