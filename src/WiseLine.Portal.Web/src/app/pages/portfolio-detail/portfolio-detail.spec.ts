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
});
