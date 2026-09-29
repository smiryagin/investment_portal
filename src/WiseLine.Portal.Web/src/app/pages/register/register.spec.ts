import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AuthService } from '../../core/auth.service';
import { RegisterPage } from './register';

describe('RegisterPage', () => {
  const auth = {
    googleLogin: vi.fn(),
    register: vi.fn(() => of()),
  };

  beforeEach(async () => {
    auth.googleLogin.mockClear();
    auth.register.mockClear();

    await TestBed.configureTestingModule({
      imports: [RegisterPage],
      providers: [provideRouter([]), { provide: AuthService, useValue: auth }],
    }).compileComponents();
  });

  it('offers Google account creation', () => {
    const fixture = TestBed.createComponent(RegisterPage);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    const googleButton = element.querySelector<HTMLButtonElement>('.google-button');

    expect(googleButton?.textContent).toContain('Continue with Google');

    googleButton?.click();

    expect(auth.googleLogin).toHaveBeenCalledWith('/account');
  });

  it('explains that a payment method starts the trial', () => {
    const fixture = TestBed.createComponent(RegisterPage);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;

    expect(element.textContent).toContain('Payment method required to start the trial');
    expect(element.textContent).toContain('No charge until the 14-day trial ends');
    expect(
      element.querySelector<HTMLButtonElement>('button[type="submit"]')?.textContent,
    ).toContain('Create account');
  });

  it('shows password requirements as the user types', () => {
    const fixture = TestBed.createComponent(RegisterPage);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    const password = element.querySelector<HTMLInputElement>('#password')!;

    password.value = 'lowercaseonly';
    password.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    expect(element.querySelectorAll('.password-requirement.met').length).toBe(2);
    expect(element.querySelectorAll('.password-requirement.unmet').length).toBe(3);
    expect(element.querySelector('.field-error')?.textContent).toContain(
      'does not meet all requirements',
    );

    password.value = 'StrongPassword1!';
    password.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    expect(element.querySelectorAll('.password-requirement.met').length).toBe(5);
    expect(element.querySelectorAll('.password-requirement.unmet').length).toBe(0);
    expect(element.querySelector('.field-error')).toBeNull();
  });

  it('does not submit an incorrectly formatted password', () => {
    const fixture = TestBed.createComponent(RegisterPage);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    const setInput = (selector: string, value: string) => {
      const input = element.querySelector<HTMLInputElement>(selector)!;
      input.value = value;
      input.dispatchEvent(new Event('input'));
    };

    setInput('#displayName', 'Andrey');
    setInput('#email', 'andrey@example.com');
    setInput('#password', 'lowercaseonly');
    element.querySelector<HTMLInputElement>('input[type="checkbox"]')!.click();
    element.querySelector<HTMLFormElement>('form')!.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(auth.register).not.toHaveBeenCalled();
  });
});
