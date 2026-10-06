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

  it('renders a cash-only portfolio as total value and cash balance', () => {
    const id = '56fe6ab6-cfbd-f111-8101-0022482049e4';
    route.snapshot.paramMap.get.mockReturnValue(id);
    api.getPortfolio.mockReturnValue(
      of({
        id,
        name: 'Cash Reserve',
        description: null,
        strategyName: null,
        marketValue: 10000,
        cashBalance: 10000,
        totalCost: 0,
        unrealizedGain: 0,
        unrealizedGainPercent: 0,
        positions: [],
      }),
    );

    const fixture = TestBed.createComponent(PortfolioDetailPage);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('.headline-value strong')?.textContent).toContain('$10,000');
    expect(element.textContent).toContain('Cash balance');
    expect(element.textContent).toContain('This portfolio has no positions yet.');
  });

  it('renders cash as part of a mixed portfolio total without changing security gains', () => {
    const id = '56fe6ab6-cfbd-f111-8101-0022482049e4';
    route.snapshot.paramMap.get.mockReturnValue(id);
    api.getPortfolio.mockReturnValue(
      of({
        id,
        name: 'Staging Acceptance Portfolio',
        description: 'WiseLine Staging',
        strategyName: 'Staging acceptance allocation',
        marketValue: 11401,
        cashBalance: 10000,
        totalCost: 1000,
        unrealizedGain: 401,
        unrealizedGainPercent: 40.1,
        positions: [
          {
            id: 'VOO',
            symbol: 'VOO',
            description: 'Vanguard S&P 500 ETF',
            quantity: 2,
            averagePrice: 500,
            currentPrice: 700.5,
            marketValue: 1401,
            unrealizedGain: 401,
            unrealizedGainPercent: 40.1,
          },
        ],
      }),
    );

    const fixture = TestBed.createComponent(PortfolioDetailPage);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('.headline-value strong')?.textContent).toContain('$11,401');
    expect(element.textContent).toContain('$10,000');
    expect(element.textContent).toContain('$401');
    expect(element.textContent).toContain('40.1%');
    expect(element.textContent).toContain('VOO');
  });
});
