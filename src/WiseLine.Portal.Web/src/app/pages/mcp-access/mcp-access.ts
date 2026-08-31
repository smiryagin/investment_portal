import { DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { McpTokenCreated, McpTokenSummary } from '../../core/api.models';
import { readableHttpError } from '../../core/http-error';
import { PortalApiService } from '../../core/portal-api.service';

@Component({
  selector: 'app-mcp-access-page',
  imports: [DatePipe, ReactiveFormsModule, RouterLink],
  templateUrl: './mcp-access.html',
  styleUrl: './mcp-access.scss',
})
export class McpAccessPage implements OnInit {
  private readonly api = inject(PortalApiService);
  private readonly formBuilder = inject(FormBuilder);
  protected readonly tokens = signal<McpTokenSummary[]>([]);
  protected readonly createdToken = signal<McpTokenCreated | null>(null);
  protected readonly loading = signal(true);
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly copied = signal(false);
  protected readonly form = this.formBuilder.nonNullable.group({
    displayName: ['', [Validators.required, Validators.maxLength(100)]],
  });

  ngOnInit(): void {
    this.load();
  }

  protected create(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    this.error.set(null);
    this.submitting.set(true);
    this.api
      .createMcpToken(this.form.controls.displayName.value)
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: (token) => {
          this.createdToken.set(token);
          this.tokens.update((tokens) => [token, ...tokens]);
          this.form.reset();
        },
        error: (error: unknown) =>
          this.error.set(readableHttpError(error, 'The token could not be created.')),
      });
  }

  protected async copyToken(): Promise<void> {
    const token = this.createdToken()?.token;
    if (!token) return;
    await navigator.clipboard.writeText(token);
    this.copied.set(true);
    window.setTimeout(() => this.copied.set(false), 1800);
  }

  protected dismissCreatedToken(): void {
    this.createdToken.set(null);
    this.copied.set(false);
  }

  protected revoke(token: McpTokenSummary): void {
    if (
      !window.confirm(
        `Revoke the “${token.displayName}” token? This client will lose access immediately.`,
      )
    ) {
      return;
    }

    this.api.revokeMcpToken(token.id).subscribe({
      next: () =>
        this.tokens.update((tokens) =>
          tokens.map((item) => (item.id === token.id ? { ...item, isRevoked: true } : item)),
        ),
      error: (error: unknown) =>
        this.error.set(readableHttpError(error, 'The token could not be revoked.')),
    });
  }

  private load(): void {
    this.api
      .getMcpTokens()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (tokens) => this.tokens.set(tokens),
        error: (error: unknown) =>
          this.error.set(readableHttpError(error, 'MCP token information is unavailable.')),
      });
  }
}
