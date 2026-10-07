import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../core/auth.service';
import { readableHttpError } from '../../core/http-error';
import { PostLoginNavigationService } from '../../core/post-login-navigation.service';

@Component({
  selector: 'app-login-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class LoginPage {
  private readonly formBuilder = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly postLoginNavigation = inject(PostLoginNavigationService);
  protected readonly auth = inject(AuthService);

  protected readonly submitting = signal(false);
  protected readonly passwordVisible = signal(false);
  protected readonly error = signal<string | null>(
    this.route.snapshot.queryParamMap.has('externalError')
      ? 'Google login could not be completed. Please try again.'
      : null,
  );

  protected readonly form = this.formBuilder.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
    rememberMe: [false],
  });

  protected submit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    this.error.set(null);
    this.submitting.set(true);
    this.auth
      .login(this.form.getRawValue())
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: () => {
          this.postLoginNavigation.navigate(this.route.snapshot.queryParamMap.get('returnUrl'));
        },
        error: (error: unknown) =>
          this.error.set(readableHttpError(error, 'Login failed. Please try again.')),
      });
  }

  protected googleLogin(): void {
    const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/dashboard';
    this.auth.googleLogin(returnUrl.startsWith('/') ? returnUrl : '/dashboard');
  }

  protected togglePasswordVisibility(): void {
    this.passwordVisible.update((visible) => !visible);
  }
}
