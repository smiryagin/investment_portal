import { Component, signal } from '@angular/core';

type Client = 'codex' | 'chatgpt' | 'other';

@Component({
  selector: 'app-docs-page',
  templateUrl: './docs.html',
  styleUrl: './docs.scss',
})
export class DocsPage {
  protected readonly selected = signal<Client>('codex');
  protected readonly copied = signal(false);
  protected readonly mcpUrl = window.location.hostname.startsWith('staging.')
    ? 'https://staging-investments-mcp.wiselinetrade.com/mcp'
    : 'https://investments-mcp.torusystems.com/mcp';
  protected readonly codexAddCommand = `codex mcp add investments --url ${this.mcpUrl}`;
  protected readonly codexLoginCommand = 'codex mcp login investments';
  protected readonly genericConfig = `{
  "name": "investments",
  "transport": "streamable-http",
  "url": "${this.mcpUrl}",
  "authentication": "oauth"
}`;

  protected select(client: Client): void {
    this.selected.set(client);
    this.copied.set(false);
  }

  protected async copy(value: string): Promise<void> {
    await navigator.clipboard.writeText(value);
    this.copied.set(true);
    window.setTimeout(() => this.copied.set(false), 1800);
  }
}
