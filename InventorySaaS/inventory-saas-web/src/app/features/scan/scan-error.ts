import { HttpErrorResponse } from '@angular/common/http';

/**
 * Turns an API failure into a line a warehouse operator can act on.
 *
 * The backend's exception middleware returns problem-details style bodies, and the domain
 * messages it produces ("Insufficient stock… Available: 2, requested: 5") are already written
 * for the operator — so they are preferred over anything generic. Network and server faults,
 * which carry nothing useful, fall back to a plain sentence.
 */
export function describeApiError(error: HttpErrorResponse): string {
  const body = error.error as { detail?: string; error?: string; title?: string } | undefined;

  const message = body?.detail ?? body?.error ?? body?.title;
  if (typeof message === 'string' && message.trim().length > 0) return message;

  // Status 0 means the request never reached the server — the usual warehouse Wi-Fi dropout.
  if (error.status === 0) return 'No connection. Check the network and try again.';
  if (error.status === 401) return 'Your session has expired. Sign in again.';
  if (error.status === 403) return 'You do not have permission to do that.';

  return 'Something went wrong. Please try again.';
}
