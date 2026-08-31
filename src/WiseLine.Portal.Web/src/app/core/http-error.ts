import { HttpErrorResponse } from '@angular/common/http';
import { ProblemDetails } from './api.models';

export function readableHttpError(error: unknown, fallback: string): string {
  if (!(error instanceof HttpErrorResponse)) {
    return fallback;
  }

  const problem = error.error as ProblemDetails | undefined;
  if (problem?.detail) {
    return problem.detail;
  }

  if (problem?.errors) {
    const first = Object.values(problem.errors).flat()[0];
    if (first) {
      return first;
    }
  }

  return problem?.title ?? fallback;
}
