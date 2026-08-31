import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { catchError, Observable, of, shareReplay, switchMap, tap } from 'rxjs';
import { CurrentUser } from './api.models';

export interface LoginInput {
  email: string;
  password: string;
  rememberMe: boolean;
}

export interface RegisterInput {
  email: string;
  password: string;
  displayName: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly currentUser = signal<CurrentUser | null | undefined>(undefined);
  private currentRequest?: Observable<CurrentUser | null>;

  readonly user = computed(() => this.currentUser() ?? null);
  readonly isAuthenticated = computed(
    () => this.currentUser() !== null && this.currentUser() !== undefined,
  );
  readonly isResolved = computed(() => this.currentUser() !== undefined);

  initialize(): void {
    this.ensureCurrent().subscribe();
  }

  ensureCurrent(): Observable<CurrentUser | null> {
    const existing = this.currentUser();
    if (existing !== undefined) {
      return of(existing);
    }

    if (!this.currentRequest) {
      this.currentRequest = this.http.get<CurrentUser>('/api/auth/me').pipe(
        tap((user) => this.currentUser.set(user)),
        catchError(() => {
          this.currentUser.set(null);
          return of(null);
        }),
        shareReplay(1),
      );
    }

    return this.currentRequest;
  }

  login(input: LoginInput): Observable<CurrentUser> {
    return this.ensureCsrf().pipe(
      switchMap(() => this.http.post<CurrentUser>('/api/auth/login', input)),
      tap((user) => this.currentUser.set(user)),
    );
  }

  register(input: RegisterInput): Observable<CurrentUser> {
    return this.ensureCsrf().pipe(
      switchMap(() => this.http.post<CurrentUser>('/api/auth/register', input)),
      tap((user) => this.currentUser.set(user)),
    );
  }

  logout(): Observable<void> {
    return this.ensureCsrf().pipe(
      switchMap(() => this.http.post<void>('/api/auth/logout', {})),
      tap(() => this.currentUser.set(null)),
    );
  }

  googleLogin(returnUrl = '/dashboard'): void {
    window.location.assign(`/api/auth/google?returnUrl=${encodeURIComponent(returnUrl)}`);
  }

  ensureCsrf(): Observable<void> {
    return this.http.get<void>('/api/security/csrf');
  }
}
