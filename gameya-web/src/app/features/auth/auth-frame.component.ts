import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'app-auth-frame',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="auth-page" id="main-content">
      <section class="auth-brand" aria-label="عن جمعية">
        <div class="brand-mark"><span class="brand-mark__logo">ج</span><span>جمعية</span></div>
        <div>
          <h1>جمعيتك<br>أوضح وأسهل.</h1>
          <p>تابع الجمعيات، المدفوعات، ومواعيد القبض من مكان واحد وبطريقة معمولة مخصوص للاستخدام اليومي.</p>
        </div>
        <small>إدارة مالية واضحة وآمنة</small>
      </section>
      <section class="auth-panel"><div class="auth-card"><ng-content /></div></section>
    </main>
  `,
})
export class AuthFrameComponent {}
