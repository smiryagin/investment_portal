import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  styleUrl: './app.scss',
  templateUrl: './app.html',
})
export class App {
  protected readonly auth = inject(AuthService);
  protected readonly menuOpen = signal(false);
  private readonly router = inject(Router);

  constructor() {
    this.auth.initialize();
  }

  protected closeMenu(): void {
    this.menuOpen.set(false);
  }

  protected toggleMenu(): void {
    this.menuOpen.update((open) => !open);
  }

  protected logout(): void {
    this.auth.logout().subscribe(() => {
      this.closeMenu();
      void this.router.navigate(['/']);
    });
  }
}
