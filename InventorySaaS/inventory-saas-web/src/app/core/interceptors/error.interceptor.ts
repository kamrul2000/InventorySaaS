import { HttpHandlerFn, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { NotificationService } from '../services/notification.service';
import { ProblemResponse } from '../models/api.models';

export const errorInterceptor: HttpInterceptorFn = (req: HttpRequest<unknown>, next: HttpHandlerFn) => {
  const notificationService = inject(NotificationService);

  return next(req).pipe(
    catchError((error) => {
      if (error.status === 0) {
        notificationService.error('Unable to connect to server. Please check your connection.');
      } else if (error.status === 403) {
        notificationService.error('You do not have permission to perform this action.');
      } else if (error.status === 404) {
        notificationService.error('The requested resource was not found.');
      } else if (error.status === 409) {
        notificationService.error('A conflict occurred. The resource may have been modified.');
      } else if (error.status === 422 || error.status === 400) {
        const problemResponse = error.error as ProblemResponse;
        if (problemResponse?.errors) {
          const messages = Object.values(problemResponse.errors).flat();
          notificationService.error(messages.join('. '));
        } else if (problemResponse?.title) {
          notificationService.error(problemResponse.title);
        } else {
          notificationService.error('A validation error occurred.');
        }
      } else if (error.status >= 500) {
        notificationService.error('A server error occurred. Please try again later.');
      }

      return throwError(() => error);
    })
  );
};
