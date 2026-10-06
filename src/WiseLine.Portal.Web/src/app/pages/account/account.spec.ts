import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { NEVER, of } from 'rxjs';
import { AuthService } from '../../core/auth.service';
import { PortalApiService } from '../../core/portal-api.service';
import { AccountPage } from './account';

describe('AccountPage', () => {
  const api = {
    getSubscription: vi.fn(),
    redeemPromotion: vi.fn(),
    createCheckout: vi.fn(),
    createBillingPortal: vi.fn(),
    getOAuthConnections: vi.fn(),
    revokeOAuthConnection: vi.fn(),
  };
  const auth = {
    user: signal({
      id: 'user-1',
      email: 'andrey@example.com',
      displayName: 'Andrey',
      hasGoogleLogin: true,
      emailConfirmed: true,
    }),
    resendEmailConfirmation: vi.fn(),
  };

  beforeEach(async () => {
    api.getSubscription.mockReturnValue(
      of({
        planKey: 'monthly',
        status: 'Trialing',
        isEntitled: true,
        trialEndsAt: '2026-10-14T00:00:00Z',
        currentPeriodEndsAt: '2026-10-14T00:00:00Z',
        cancelAtPeriodEnd: false,
        paymentProvider: 'Stripe',
      }),
    );
    api.createBillingPortal.mockReturnValue(NEVER);
    api.getOAuthConnections.mockReturnValue(of([]));
    api.createBillingPortal.mockClear();

    await TestBed.configureTestingModule({
      imports: [AccountPage],
      providers: [
        { provide: PortalApiService, useValue: api },
        { provide: AuthService, useValue: auth },
      ],
    }).compileComponents();
  });

  it('offers Stripe billing management for a trialing subscriber', () => {
    const fixture = TestBed.createComponent(AccountPage);
    fixture.detectChanges();

    const button = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>(
      '.billing-actions button',
    );

    expect(button?.textContent).toContain('Manage billing');
    button?.click();
    fixture.detectChanges();

    expect(api.createBillingPortal).toHaveBeenCalledOnce();
    expect(button?.textContent).toContain('Opening');
  });
});
