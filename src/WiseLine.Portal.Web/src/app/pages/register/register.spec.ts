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
});
