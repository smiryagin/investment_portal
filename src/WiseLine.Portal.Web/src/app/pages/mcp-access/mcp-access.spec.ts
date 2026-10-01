import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { PortalApiService } from '../../core/portal-api.service';
import { McpAccessPage } from './mcp-access';

describe('McpAccessPage', () => {
  const api = {
    getMcpTokens: vi.fn(),
    createMcpToken: vi.fn(),
    revokeMcpToken: vi.fn(),
  };

  beforeEach(async () => {
    vi.clearAllMocks();
    api.getMcpTokens.mockReturnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 402,
            statusText: 'Payment Required',
            url: '/api/mcp-tokens',
            error: {
              detail: 'Add a payment method to start your 14-day trial and manage MCP access.',
            },
          }),
      ),
    );

    await TestBed.configureTestingModule({
      imports: [McpAccessPage],
      providers: [provideRouter([]), { provide: PortalApiService, useValue: api }],
    }).compileComponents();
  });

  it('directs a pending subscriber to add a payment method', () => {
    const fixture = TestBed.createComponent(McpAccessPage);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    const callout = element.querySelector<HTMLElement>('.trial-required');
    const accountLink = callout?.querySelector<HTMLAnchorElement>('a');

    expect(callout?.textContent).toContain('Start your 14-day free trial');
    expect(callout?.textContent).toContain('You will not be charged today');
    expect(accountLink?.getAttribute('href')).toBe('/account');
    expect(element.querySelector('.form-error')).toBeNull();
    expect(element.querySelector('.create-panel form')).toBeNull();
  });

  it('rejects an active token name regardless of casing or surrounding whitespace', () => {
    api.getMcpTokens.mockReturnValue(
      of([
        {
          id: 'b343929d-4ff4-42a4-af73-106ac3af7c95',
          displayName: 'Codex laptop',
          prefix: 'imcp_123456789012',
          createdAt: '2026-10-01T12:00:00Z',
          lastUsedAt: null,
          expiresAt: '2999-10-15T12:00:00Z',
          isRevoked: false,
        },
      ]),
    );

    const fixture = TestBed.createComponent(McpAccessPage);
    fixture.detectChanges();
    const component = fixture.componentInstance as unknown as {
      form: {
        controls: {
          displayName: {
            setValue(value: string): void;
            markAsTouched(): void;
          };
        };
      };
      create(): void;
    };

    component.form.controls.displayName.setValue('  CODEX LAPTOP  ');
    component.form.controls.displayName.markAsTouched();
    component.create();
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('.field-error')?.textContent).toContain(
      'already have an active token with this name',
    );
    expect(api.createMcpToken).not.toHaveBeenCalled();
  });

  it('allows a name to be reused after the previous token expires', () => {
    api.getMcpTokens.mockReturnValue(
      of([
        {
          id: 'cd695f75-d75f-4726-a0f2-bc60302466d0',
          displayName: 'Codex laptop',
          prefix: 'imcp_123456789012',
          createdAt: '2000-09-01T12:00:00Z',
          lastUsedAt: null,
          expiresAt: '2000-09-15T12:00:00Z',
          isRevoked: false,
        },
      ]),
    );
    api.createMcpToken.mockReturnValue(
      of({
        id: '3dac5c80-27a4-4527-8948-e280ec9c39a6',
        displayName: 'Codex laptop',
        prefix: 'imcp_abcdefghijkl',
        token: 'imcp_test-token',
        createdAt: '2026-10-01T12:00:00Z',
        lastUsedAt: null,
        expiresAt: '2026-10-15T12:00:00Z',
        isRevoked: false,
      }),
    );

    const fixture = TestBed.createComponent(McpAccessPage);
    fixture.detectChanges();
    const component = fixture.componentInstance as unknown as {
      form: { controls: { displayName: { setValue(value: string): void } } };
      create(): void;
    };

    component.form.controls.displayName.setValue('Codex laptop');
    component.create();

    expect(api.createMcpToken).toHaveBeenCalledWith('Codex laptop');
  });
});
