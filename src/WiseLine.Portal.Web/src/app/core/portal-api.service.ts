import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, switchMap } from 'rxjs';
import {
  CheckoutResponse,
  OAuthConnection,
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

  getOAuthConnections(): Observable<OAuthConnection[]> {
    return this.http.get<OAuthConnection[]>('/api/oauth/connections');
  }

  revokeOAuthConnection(id: string): Observable<void> {
    return this.auth
      .ensureCsrf()
      .pipe(switchMap(() => this.http.delete<void>(`/api/oauth/connections/${id}`)));
  }

  createCheckout(provider: 'Stripe' | 'PayPal'): Observable<CheckoutResponse> {
    return this.auth
      .ensureCsrf()
      .pipe(
        switchMap(() => this.http.post<CheckoutResponse>(`/api/payments/checkout/${provider}`, {})),
      );
  }

  createBillingPortal(): Observable<CheckoutResponse> {
    return this.auth
      .ensureCsrf()
      .pipe(switchMap(() => this.http.post<CheckoutResponse>('/api/payments/billing-portal', {})));
  }
}
