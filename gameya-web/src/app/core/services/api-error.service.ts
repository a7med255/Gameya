import { Injectable } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { ProblemDetails } from '../models/api.models';

const STATUS_MESSAGES: Readonly<Record<number, string>> = {
  0: 'تعذر الاتصال بالخدمة. راجع الإنترنت وحاول تاني.',
  400: 'راجع البيانات المدخلة وحاول تاني.',
  401: 'انتهت جلستك. سجّل دخولك من جديد.',
  403: 'ليس لديك صلاحية للوصول إلى هذه البيانات.',
  404: 'البيانات المطلوبة غير موجودة.',
  409: 'تعذر تنفيذ العملية بسبب تعارض في البيانات.',
  422: 'بعض البيانات غير صحيحة. راجعها وحاول تاني.',
  429: 'محاولات كتير في وقت قصير. استنى شوية وحاول تاني.',
  500: 'حصلت مشكلة غير متوقعة. حاول تاني بعد شوية.',
};

@Injectable({ providedIn: 'root' })
export class ApiErrorService {
  message(error: unknown): string {
    if (!(error instanceof HttpErrorResponse)) return 'حصلت مشكلة غير متوقعة. حاول تاني.';
    const problem = isProblemDetails(error.error) ? error.error : null;
    return problem?.detail || problem?.title || STATUS_MESSAGES[error.status] || 'تعذر تنفيذ العملية. حاول تاني.';
  }

  fieldErrors(error: unknown): Readonly<Record<string, readonly string[]>> {
    if (!(error instanceof HttpErrorResponse) || !isProblemDetails(error.error)) return {};
    return error.error.errors ?? {};
  }
}

function isProblemDetails(value: unknown): value is ProblemDetails {
  return typeof value === 'object' && value !== null;
}
