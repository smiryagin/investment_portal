import { TestBed } from '@angular/core/testing';
import { DocsPage } from './docs';

describe('DocsPage', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DocsPage],
    }).compileComponents();
  });

  it('shows dedicated OAuth setup for Claude, Gemini, and OpenCode', () => {
    const fixture = TestBed.createComponent(DocsPage);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;

    clickClient(element, 'Claude Code');
    fixture.detectChanges();
    expect(element.textContent).toContain('--client-id wiseline-claude-code');
    expect(element.textContent).toContain('claude mcp login investments');

    clickClient(element, 'Gemini CLI');
    fixture.detectChanges();
    expect(element.textContent).toContain('wiseline-gemini-cli');
    expect(element.textContent).toContain('http://localhost:47633/oauth/callback');

    clickClient(element, 'OpenCode');
    fixture.detectChanges();
    expect(element.textContent).toContain('wiseline-opencode');
    expect(element.textContent).toContain('opencode mcp auth investments');
  });
});

function clickClient(element: HTMLElement, label: string): void {
  const button = [...element.querySelectorAll<HTMLButtonElement>('.docs-sidebar button')].find(
    (candidate) => candidate.textContent?.trim() === label,
  );
  expect(button).toBeDefined();
  button?.click();
}
