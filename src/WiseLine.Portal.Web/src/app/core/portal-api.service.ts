import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, switchMap } from 'rxjs';
import {
  CheckoutResponse,
  McpTokenCreated,
  McpTokenSummary,
  PortfolioDetails,
  PortfolioSummary,
  Subscription,
} from './api.models';
import { AuthService } from './auth.service';

@Injectable({ providedIn: 'root' })
export class PortalApiService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);

  getSubscription(): Observable<Subscription> {
    return this.http.get<Subscription>('/api/subscription');
  }

  redeemPromotion(code: string): Observable<Subscription> {
    return this.auth
      .ensureCsrf()
      .pipe(switchMap(() => this.http.post<Subscription>('/api/subscription/promotion', { code })));
  }

  getPortfolios(): Observable<PortfolioSummary[]> {
    return this.http.get<PortfolioSummary[]>('/api/portfolios');
  }

  getPortfolio(id: string): Observable<PortfolioDetails> {
    return this.http.get<PortfolioDetails>(`/api/portfolios/${id}`);
  }

  getMcpTokens(): Observable<McpTokenSummary[]> {
    return this.http.get<McpTokenSummary[]>('/api/mcp-tokens');
  }

  createMcpToken(displayName: string): Observable<McpTokenCreated> {
    return this.auth
      .ensureCsrf()
      .pipe(switchMap(() => this.http.post<McpTokenCreated>('/api/mcp-tokens', { displayName })));
  }

  revokeMcpToken(id: string): Observable<void> {
    return this.auth
      .ensureCsrf()
      .pipe(switchMap(() => this.http.delete<void>(`/api/mcp-tokens/${id}`)));
  }

  createCheckout(provider: 'Stripe' | 'PayPal'): Observable<CheckoutResponse> {
    return this.auth
      .ensureCsrf()
      .pipe(
        switchMap(() => this.http.post<CheckoutResponse>(`/api/payments/checkout/${provider}`, {})),
      );
  }
}
