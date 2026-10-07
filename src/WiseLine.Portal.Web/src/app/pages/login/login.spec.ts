import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AuthService } from '../../core/auth.service';
import {
  normalizeLocalReturnUrl,
  PostLoginNavigationService,
} from '../../core/post-login-navigation.service';
import { LoginPage } from './login';

describe('LoginPage', () => {
  const oauthReturnUrl =
    '/connect/authorize?response_type=code&client_id=wiseline-codex-cli&redirect_uri=http%3A%2F%2F127.0.0.1%3A53108%2Fcallback';
  const auth = {
    googleLogin: vi.fn(),
    login: vi.fn(() =>
      of({
        id: '72bd1333-1954-4d79-8bab-3e25338bd0ce',
        email: 'andrey@example.com',
        displayName: 'Andrey',
        hasGoogleLogin: false,
        emailConfirmed: true,
      }),
    ),
  };
  const postLoginNavigation = {
    navigate: vi.fn(),
  };

  beforeEach(async () => {
    auth.googleLogin.mockClear();
    auth.login.mockClear();
    postLoginNavigation.navigate.mockClear();

    await TestBed.configureTestingModule({
      imports: [LoginPage],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: auth },
        { provide: PostLoginNavigationService, useValue: postLoginNavigation },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              queryParamMap: convertToParamMap({ returnUrl: oauthReturnUrl }),
            },
          },
        },
      ],
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

  it('resumes a server OAuth request with a full-page navigation after login', () => {
    const fixture = TestBed.createComponent(LoginPage);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    const setInput = (selector: string, value: string) => {
      const input = element.querySelector<HTMLInputElement>(selector)!;
      input.value = value;
      input.dispatchEvent(new Event('input'));
    };

    setInput('#email', 'andrey@example.com');
    setInput('#password', 'StrongPassword1!');
    element.querySelector<HTMLFormElement>('form')!.dispatchEvent(new Event('submit'));

    expect(auth.login).toHaveBeenCalledOnce();
    expect(postLoginNavigation.navigate).toHaveBeenCalledWith(oauthReturnUrl);
  });

  it('only permits same-origin post-login destinations', () => {
    expect(normalizeLocalReturnUrl(oauthReturnUrl, 'https://staging.wiselinetrade.com')).toBe(
      oauthReturnUrl,
    );
    expect(
      normalizeLocalReturnUrl('//attacker.example/path', 'https://staging.wiselinetrade.com'),
    ).toBe('/dashboard');
    expect(
      normalizeLocalReturnUrl('/\\attacker.example/path', 'https://staging.wiselinetrade.com'),
    ).toBe('/dashboard');
  });
});
