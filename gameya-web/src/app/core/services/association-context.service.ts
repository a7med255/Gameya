import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class AssociationContextService {
  private readonly accessibleIdsState = signal<ReadonlySet<string>>(new Set());
  readonly accessibleIds = this.accessibleIdsState.asReadonly();

  setAccessible(ids: readonly string[]): void { this.accessibleIdsState.set(new Set(ids)); }
  canAccess(id: string): boolean { return this.accessibleIdsState().has(id); }
}
