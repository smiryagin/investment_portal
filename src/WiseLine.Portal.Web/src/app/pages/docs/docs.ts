import { Component, signal } from '@angular/core';

type Client = 'codex' | 'claude' | 'other';

@Component({
  selector: 'app-docs-page',
  templateUrl: './docs.html',
  styleUrl: './docs.scss',
})
export class DocsPage {
  protected readonly selected = signal<Client>('codex');
  protected readonly copied = signal(false);

  protected readonly codexCommand = `codex mcp add investments --url https://investments-mcp.torusystems.com/mcp --bearer-token-env-var INVESTMENTS_MCP_TOKEN`;
  protected readonly claudeConfig = `{
  "mcpServers": {
    "investments": {
      "type": "http",
      "url": "https://investments-mcp.torusystems.com/mcp",
      "headers": {
        "Authorization": "Bearer \${INVESTMENTS_MCP_TOKEN}"
      }
    }
  }
}`;
  protected readonly genericConfig = `{
  "name": "investments",
  "transport": "streamable-http",
  "url": "https://investments-mcp.torusystems.com/mcp",
  "headers": {
    "Authorization": "Bearer <YOUR_MCP_TOKEN>"
  }
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
