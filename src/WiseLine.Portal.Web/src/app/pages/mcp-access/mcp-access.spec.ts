import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { throwError } from 'rxjs';
import { PortalApiService } from '../../core/portal-api.service';
import { McpAccessPage } from './mcp-access';

describe('McpAccessPage', () => {
  const api = {
    getMcpTokens: vi.fn(),
    createMcpToken: vi.fn(),
    revokeMcpToken: vi.fn(),
  };

  beforeEach(async () => {
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
});
