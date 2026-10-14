import { Injectable, signal } from '@angular/core';

export interface ConfirmOptions {
  title: string;
  message: string;
  confirmLabel?: string;
  danger?: boolean;
}

export interface ConfirmState extends ConfirmOptions {
  resolve: (answer: boolean) => void;
}

@Injectable({ providedIn: 'root' })
export class ConfirmService {
  private readonly state = signal<ConfirmState | null>(null);
  readonly requestState = this.state.asReadonly();

  ask(options: ConfirmOptions): Promise<boolean> {
    return new Promise<boolean>((resolve) => this.state.set({ ...options, resolve }));
  }

  answer(value: boolean): void {
    const current = this.state();
    if (!current) return;
    this.state.set(null);
    current.resolve(value);
  }
}
