import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { PortalApiService } from '../../core/portal-api.service';
import { PortfolioDetailPage } from './portfolio-detail';

describe('PortfolioDetailPage', () => {
  const api = {
    getPortfolio: vi.fn(),
  };
  const route = {
    snapshot: {
      paramMap: {
        get: vi.fn(),
      },
    },
  };

  beforeEach(async () => {
    vi.clearAllMocks();
    api.getPortfolio.mockReturnValue(of({}));

    await TestBed.configureTestingModule({
      imports: [PortfolioDetailPage],
      providers: [
        provideRouter([]),
        { provide: PortalApiService, useValue: api },
        { provide: ActivatedRoute, useValue: route },
      ],
    }).compileComponents();
  });

  it('accepts a SQL Server-generated uniqueidentifier', () => {
    const id = '56fe6ab6-cfbd-f111-8101-0022482049e4';
    route.snapshot.paramMap.get.mockReturnValue(id);

    const fixture = TestBed.createComponent(PortfolioDetailPage);
    fixture.componentInstance.ngOnInit();

    expect(api.getPortfolio).toHaveBeenCalledWith(id);
  });

  it('rejects a malformed portfolio identifier', () => {
    route.snapshot.paramMap.get.mockReturnValue('not-a-guid');

    const fixture = TestBed.createComponent(PortfolioDetailPage);
    fixture.componentInstance.ngOnInit();

    expect(api.getPortfolio).not.toHaveBeenCalled();
  });

  it('shows portfolio-specific and global strategy details', () => {
    route.snapshot.paramMap.get.mockReturnValue('56fe6ab6-cfbd-f111-8101-0022482049e4');
    api.getPortfolio.mockReturnValue(
      of({
        id: '56fe6ab6-cfbd-f111-8101-0022482049e4',
        name: 'Retirement',
        description: 'Long-term account',
        strategyName: 'Long-term growth',
        marketValue: 100000,
        totalCost: 80000,
        unrealizedGain: 20000,
        unrealizedGainPercent: 25,
        positions: [],
        strategies: [
          {
            id: '11111111-1111-1111-1111-111111111111',
            accountId: '56fe6ab6-cfbd-f111-8101-0022482049e4',
            name: 'Long-term growth',
            type: 'allocation',
            rule: { targetEquityPercent: 70, rebalanceAnnually: true },
            scope: 'portfolio',
            updatedAt: '2026-10-09T12:00:00Z',
          },
          {
            id: '22222222-2222-2222-2222-222222222222',
            accountId: null,
            name: 'Avoid leverage',
            type: 'constraint',
            rule: { allowMargin: false },
            scope: 'global',
            updatedAt: '2026-10-09T12:00:00Z',
          },
        ],
      }),
    );

    const fixture = TestBed.createComponent(PortfolioDetailPage);
    fixture.detectChanges();
    const content = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(content).toContain('Portfolio strategy');
    expect(content).toContain('Long-term growth');
    expect(content).toContain('Target Equity Percent');
    expect(content).toContain('70');
    expect(content).toContain('Portfolio-specific');
    expect(content).toContain('Avoid leverage');
    expect(content).toContain('Applies to all portfolios');
    expect(content).toContain('Allow Margin');
    expect(content).toContain('No');
  });
});
