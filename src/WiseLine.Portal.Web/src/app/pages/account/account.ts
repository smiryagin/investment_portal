import { DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { Subscription } from '../../core/api.models';
import { AuthService } from '../../core/auth.service';
import { readableHttpError } from '../../core/http-error';
import { PortalApiService } from '../../core/portal-api.service';

@Component({
  selector: 'app-account-page',
  imports: [DatePipe, ReactiveFormsModule],
  templateUrl: './account.html',
  styleUrl: './account.scss',
})
export class AccountPage implements OnInit {
  private readonly api = inject(PortalApiService);
  private readonly formBuilder = inject(FormBuilder);
  protected readonly auth = inject(AuthService);
  protected readonly subscription = signal<Subscription | null>(null);
  protected readonly loading = signal(true);
  protected readonly submitting = signal(false);
  protected readonly checkoutProvider = signal<'Stripe' | 'PayPal' | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly success = signal<string | null>(null);
  protected readonly promotionForm = this.formBuilder.nonNullable.group({
    code: ['', [Validators.required, Validators.maxLength(64)]],
  });

  ngOnInit(): void {
    this.api
      .getSubscription()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (subscription) => this.subscription.set(subscription),
        error: (error: unknown) =>
          this.error.set(readableHttpError(error, 'Subscription information is unavailable.')),
      });
  }

  protected redeem(): void {
    if (this.promotionForm.invalid || this.submitting()) return;
    this.error.set(null);
    this.success.set(null);
    this.submitting.set(true);
    this.api
      .redeemPromotion(this.promotionForm.controls.code.value)
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: (subscription) => {
          this.subscription.set(subscription);
          this.promotionForm.reset();
          this.success.set('Promotion code applied successfully.');
        },
        error: (error: unknown) =>
          this.error.set(readableHttpError(error, 'Promotion code could not be applied.')),
      });
  }

  protected checkout(provider: 'Stripe' | 'PayPal'): void {
    if (this.checkoutProvider()) return;
    this.error.set(null);
    this.checkoutProvider.set(provider);
    this.api
      .createCheckout(provider)
      .pipe(finalize(() => this.checkoutProvider.set(null)))
      .subscribe({
        next: (checkout) => window.location.assign(checkout.redirectUrl),
        error: (error: unknown) =>
          this.error.set(readableHttpError(error, `${provider} checkout is unavailable.`)),
      });
  }
}
