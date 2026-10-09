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

  protected strategyEntries(rule: unknown): Array<{ label: string; value: string }> {
    if (rule === null || rule === undefined) {
      return [];
    }
    if (typeof rule !== 'object' || Array.isArray(rule)) {
      return [{ label: 'Rule', value: this.formatStrategyValue(rule) }];
    }
    return Object.entries(rule as Record<string, unknown>).map(([key, value]) => ({
      label: key
        .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
        .replace(/[_-]+/g, ' ')
        .replace(/^./, (character) => character.toUpperCase()),
      value: this.formatStrategyValue(value),
    }));
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
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

  private formatStrategyValue(value: unknown): string {
    if (Array.isArray(value)) {
      return value.map((item) => this.formatStrategyValue(item)).join(', ');
    }
    if (value !== null && typeof value === 'object') {
      return JSON.stringify(value);
    }
    if (typeof value === 'boolean') {
      return value ? 'Yes' : 'No';
    }
    return String(value ?? 'Not specified');
  }
}
