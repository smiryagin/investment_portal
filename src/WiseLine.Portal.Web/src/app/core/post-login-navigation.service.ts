import { Injectable } from '@angular/core';

const defaultReturnUrl = '/dashboard';

export function normalizeLocalReturnUrl(
  returnUrl: string | null | undefined,
  origin: string,
): string {
  if (!returnUrl?.startsWith('/')) {
    return defaultReturnUrl;
  }

  try {
    const destination = new URL(returnUrl, origin);
    if (destination.origin !== origin) {
      return defaultReturnUrl;
    }

    return `${destination.pathname}${destination.search}${destination.hash}`;
  } catch {
    return defaultReturnUrl;
  }
}

@Injectable({ providedIn: 'root' })
export class PostLoginNavigationService {
  navigate(returnUrl: string | null | undefined): void {
    window.location.assign(normalizeLocalReturnUrl(returnUrl, window.location.origin));
  }
}
