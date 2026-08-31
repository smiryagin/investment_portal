import { CurrencyPipe, DecimalPipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { PortfolioDetails } from '../../core/api.models';
import { readableHttpError } from '../../core/http-error';
import { PortalApiService } from '../../core/portal-api.service';

@Component({
  selector: 'app-portfolio-detail-page',
  imports: [CurrencyPipe, DecimalPipe, RouterLink],
  templateUrl: './portfolio-detail.html',
  styleUrl: './portfolio-detail.scss',
})
export class PortfolioDetailPage implements OnInit {
  private readonly api = inject(PortalApiService);
  private readonly route = inject(ActivatedRoute);
  protected readonly portfolio = signal<PortfolioDetails | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (!Number.isSafeInteger(id) || id <= 0) {
      this.error.set('The portfolio identifier is invalid.');
      this.loading.set(false);
      return;
    }

    this.api
      .getPortfolio(id)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (portfolio) => this.portfolio.set(portfolio),
        error: (error: unknown) =>
          this.error.set(readableHttpError(error, 'Portfolio details are unavailable.')),
      });
  }
}
