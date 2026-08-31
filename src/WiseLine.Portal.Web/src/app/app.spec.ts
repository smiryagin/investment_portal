import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
  });

  it('creates the responsive application shell', () => {
    const fixture = TestBed.createComponent(App);
    const http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    http.expectOne('/api/auth/me').flush(null, { status: 401, statusText: 'Unauthorized' });
    fixture.detectChanges();

    expect(fixture.componentInstance).toBeTruthy();
    expect((fixture.nativeElement as HTMLElement).querySelector('.brand')?.textContent).toContain(
      'WiseLine',
    );
    http.verify();
  });
});
