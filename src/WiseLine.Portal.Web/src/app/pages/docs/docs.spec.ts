import { TestBed } from '@angular/core/testing';
import { DocsPage } from './docs';

describe('DocsPage', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DocsPage],
    }).compileComponents();
  });

  it('leads with the graphical ChatGPT connection flow', () => {
    const fixture = TestBed.createComponent(DocsPage);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.textContent).toContain('Recommended · No terminal required');
    expect(element.textContent).toContain('Plugins');
    expect(element.textContent).toContain('Add custom MCP server');
  });

  it('shows graphical Claude setup before the advanced Claude Code commands', () => {
    const fixture = TestBed.createComponent(DocsPage);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;

    clickClient(element, 'Claude');
    fixture.detectChanges();
    expect(element.textContent).toContain('Customize');
    expect(element.textContent).toContain('Add custom connector');
    expect(element.textContent).toContain("Use Claude's published identity (Recommended)");
    expect(element.textContent).toContain('Advanced: connect from Claude Code');
    expect(element.textContent).toContain('--client-id wiseline-claude-code');
    expect(element.textContent).toContain('claude mcp login investments');
  });

  it('explains one-time setup for Codex Desktop, Gemini, and OpenCode', () => {
    const fixture = TestBed.createComponent(DocsPage);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;

    clickClient(element, 'Codex Desktop');
    fixture.detectChanges();
    expect(element.textContent).toContain('share the same MCP configuration');
    expect(element.textContent).toContain('codex mcp login investments');

    clickClient(element, 'Gemini CLI');
    fixture.detectChanges();
    expect(element.textContent).toContain('wiseline-gemini-cli');
    expect(element.textContent).toContain('http://localhost:47633/oauth/callback');

    clickClient(element, 'OpenCode');
    fixture.detectChanges();
    expect(element.textContent).toContain('automatically uses its published OAuth identity');
    expect(element.textContent).not.toContain('wiseline-opencode');
    expect(element.textContent).toContain('opencode mcp auth investments');
  });
});

function clickClient(element: HTMLElement, label: string): void {
  const button = [...element.querySelectorAll<HTMLButtonElement>('.docs-sidebar button')].find(
    (candidate) => candidate.querySelector('strong')?.textContent?.trim() === label,
  );
  expect(button).toBeDefined();
  button?.click();
}
