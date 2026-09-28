import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { LoginPage } from './login';

describe('LoginPage', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LoginPage],
      providers: [provideRouter([]), provideHttpClient()],
    }).compileComponents();
  });

  it('renders the account access experience', () => {
    const fixture = TestBed.createComponent(LoginPage);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('h1')?.textContent).toContain('clearer portfolio decisions');
    expect(element.querySelector('h2')?.textContent).toContain('Log in to WiseLine');
    expect(element.querySelector('.google-button')?.textContent).toContain('Continue with Google');
  });

  it('allows the password to be shown and hidden', () => {
    const fixture = TestBed.createComponent(LoginPage);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    const password = element.querySelector<HTMLInputElement>('#password');
    const toggle = element.querySelector<HTMLButtonElement>('.password-toggle');

    expect(password?.type).toBe('password');

    toggle?.click();
    fixture.detectChanges();

    expect(password?.type).toBe('text');
    expect(toggle?.getAttribute('aria-pressed')).toBe('true');

    toggle?.click();
    fixture.detectChanges();

    expect(password?.type).toBe('password');
  });
});
