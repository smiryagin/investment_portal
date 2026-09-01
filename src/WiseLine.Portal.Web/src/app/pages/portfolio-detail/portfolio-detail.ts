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
    const id = this.route.snapshot.paramMap.get('id');
    const guidPattern =
      /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
    if (!id || !guidPattern.test(id)) {
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
