import { HttpHandlerFn, HttpInterceptorFn, HttpRequest } from '@angular/common/http';

export const tenantInterceptor: HttpInterceptorFn = (req: HttpRequest<unknown>, next: HttpHandlerFn) => {
  const tenantId = localStorage.getItem('tenantId');

  if (tenantId) {
    req = req.clone({
      setHeaders: {
        'X-TenantId': tenantId,
      },
    });
  }

  return next(req);
};
