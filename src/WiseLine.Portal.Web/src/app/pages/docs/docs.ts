import { Component, signal } from '@angular/core';

type Client = 'codex' | 'chatgpt' | 'claude' | 'gemini' | 'opencode' | 'other';

@Component({
  selector: 'app-docs-page',
  templateUrl: './docs.html',
  styleUrl: './docs.scss',
})
export class DocsPage {
  protected readonly selected = signal<Client>('codex');
  protected readonly copied = signal(false);
  protected readonly chatGptConnectionName = 'WiseLine Trade Investments';
  protected readonly chatGptConnectionDescription =
    'Secure portfolio, investment research, and planning tools from WiseLine Trade.';
  protected readonly mcpUrl = window.location.hostname.startsWith('staging.')
    ? 'https://staging-investments-mcp.wiselinetrade.com/mcp'
    : 'https://investments-mcp.torusystems.com/mcp';
  protected readonly codexAddCommand =
    `codex mcp add investments --url ${this.mcpUrl} ` + '--oauth-client-id wiseline-codex-cli';
  protected readonly claudeAddCommand =
    'claude mcp add --transport http --scope user ' +
    '--client-id wiseline-claude-code --callback-port 47632 ' +
    `investments ${this.mcpUrl}`;
  protected readonly claudeLoginCommand = 'claude mcp login investments';
  protected readonly geminiConfig = `{
  "mcpServers": {
    "investments": {
      "httpUrl": "${this.mcpUrl}",
      "oauth": {
        "enabled": true,
        "clientId": "wiseline-gemini-cli",
        "scopes": [
          "openid",
          "offline_access",
          "investments.read",
          "investments.write"
        ],
        "redirectUri": "http://localhost:47633/oauth/callback"
      }
    }
  }
}`;
  protected readonly opencodeConfig = `{
  "$schema": "https://opencode.ai/config.json",
  "mcp": {
    "investments": {
      "type": "remote",
      "url": "${this.mcpUrl}",
      "oauth": {
        "clientId": "wiseline-opencode",
        "scope": "openid offline_access investments.read investments.write"
      }
    }
  }
}`;
  protected readonly opencodeLoginCommand = 'opencode mcp auth investments';
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
