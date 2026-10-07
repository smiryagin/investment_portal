import { DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { OAuthConnection } from '../../core/api.models';
import { readableHttpError } from '../../core/http-error';
import { PortalApiService } from '../../core/portal-api.service';

@Component({
  selector: 'app-mcp-access-page',
  imports: [DatePipe, RouterLink],
  templateUrl: './mcp-access.html',
  styleUrl: './mcp-access.scss',
})
export class McpAccessPage implements OnInit {
  private readonly api = inject(PortalApiService);
  protected readonly connections = signal<OAuthConnection[]>([]);
  protected readonly loading = signal(true);
  protected readonly paymentRequired = signal(false);
  protected readonly revokingConnectionId = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly success = signal<string | null>(null);
  protected readonly copied = signal(false);
  protected readonly mcpUrl = window.location.hostname.startsWith('staging.')
    ? 'https://staging-investments-mcp.wiselinetrade.com/mcp'
    : 'https://investments-mcp.torusystems.com/mcp';
  protected readonly codexCommand =
    `codex mcp add investments --url ${this.mcpUrl} ` + '--oauth-client-id wiseline-codex-cli';

  ngOnInit(): void {
    this.api.getSubscription().subscribe({
      next: (subscription) => this.paymentRequired.set(!subscription.isEntitled),
      error: (error: unknown) =>
        this.error.set(readableHttpError(error, 'Subscription information is unavailable.')),
    });
    this.loadConnections();
  }

  protected async copy(value: string): Promise<void> {
    await navigator.clipboard.writeText(value);
    this.copied.set(true);
    window.setTimeout(() => this.copied.set(false), 1800);
  }

  protected revoke(connection: OAuthConnection): void {
    if (this.revokingConnectionId()) return;
    if (
      !window.confirm(
        `Disconnect ${connection.displayName}? It will lose access within 10 minutes.`,
      )
    ) {
      return;
    }

    this.error.set(null);
    this.success.set(null);
    this.revokingConnectionId.set(connection.id);
    this.api
      .revokeOAuthConnection(connection.id)
      .pipe(finalize(() => this.revokingConnectionId.set(null)))
      .subscribe({
        next: () => {
          this.connections.update((connections) =>
            connections.filter((candidate) => candidate.id !== connection.id),
          );
          this.success.set(
            `${connection.displayName} was disconnected. Existing access expires within 10 minutes.`,
          );
        },
        error: (error: unknown) =>
          this.error.set(readableHttpError(error, 'The AI connection could not be revoked.')),
      });
  }

  private loadConnections(): void {
    this.api
      .getOAuthConnections()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (connections) => this.connections.set(connections),
        error: (error: unknown) =>
          this.error.set(readableHttpError(error, 'Connected AI clients are unavailable.')),
      });
  }
}
