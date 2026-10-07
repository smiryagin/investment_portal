import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { PortalApiService } from '../../core/portal-api.service';
import { McpAccessPage } from './mcp-access';

describe('McpAccessPage', () => {
  const api = {
    getSubscription: vi.fn(),
    getOAuthConnections: vi.fn(),
    revokeOAuthConnection: vi.fn(),
  };

  beforeEach(async () => {
    vi.clearAllMocks();
    api.getSubscription.mockReturnValue(
      of({
        planKey: 'monthly',
        status: 'Trialing',
        isEntitled: true,
        trialEndsAt: '2026-10-21T12:00:00Z',
        currentPeriodEndsAt: null,
        cancelAtPeriodEnd: false,
        paymentProvider: 'Stripe',
      }),
    );
    api.getOAuthConnections.mockReturnValue(of([]));

    await TestBed.configureTestingModule({
      imports: [McpAccessPage],
      providers: [provideRouter([]), { provide: PortalApiService, useValue: api }],
    }).compileComponents();
  });

  it('shows OAuth setup without any manual token controls', () => {
    const fixture = TestBed.createComponent(McpAccessPage);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Connect without a token');
    expect(text).toContain('codex mcp login investments');
    expect(text).not.toContain('Create token');
    expect(text).not.toContain('Your access tokens');
  });

  it('directs a pending subscriber to add a payment method', () => {
    api.getSubscription.mockReturnValue(
      of({
        planKey: 'monthly',
        status: 'Pending',
        isEntitled: false,
        trialEndsAt: null,
        currentPeriodEndsAt: null,
        cancelAtPeriodEnd: false,
        paymentProvider: null,
      }),
    );

    const fixture = TestBed.createComponent(McpAccessPage);
    fixture.detectChanges();

    const callout = (fixture.nativeElement as HTMLElement).querySelector('.trial-required');
    expect(callout?.textContent).toContain('Start your 14-day free trial');
    expect(callout?.querySelector('a')?.getAttribute('href')).toBe('/account');
  });

  it('lists authorized clients', () => {
    api.getOAuthConnections.mockReturnValue(
      of([
        {
          id: 'connection-1',
          clientId: 'wiseline-codex-cli',
          displayName: 'Codex CLI',
          scopes: ['investments.read', 'investments.write'],
          authorizedAt: '2026-10-07T12:00:00Z',
        },
      ]),
    );

    const fixture = TestBed.createComponent(McpAccessPage);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Codex CLI');
    expect(text).toContain('investments.read');
    expect(text).toContain('Disconnect');
  });
});
