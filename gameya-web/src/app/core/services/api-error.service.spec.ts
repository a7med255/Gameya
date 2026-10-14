import { HttpErrorResponse } from '@angular/common/http';
import { ApiErrorService } from './api-error.service';

describe('ApiErrorService', () => {
  const service = new ApiErrorService();

  it('maps authorization failures to safe Arabic text', () => {
    expect(service.message(new HttpErrorResponse({ status: 403 }))).toBe('ليس لديك صلاحية للوصول إلى هذه البيانات.');
  });

  it('uses localized Problem Details without exposing a stack trace', () => {
    const error = new HttpErrorResponse({ status: 409, error: { title: 'تعارض', detail: 'تم تعديل البيانات من مستخدم آخر.', traceId: 'trace' } });
    expect(service.message(error)).toBe('تم تعديل البيانات من مستخدم آخر.');
  });
});
