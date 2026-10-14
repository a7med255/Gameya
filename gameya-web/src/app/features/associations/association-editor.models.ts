import { AssociationEditorMemberInput, AssociationEditorPayoutInput } from '../../core/models/api.models';

export type EditorMember = AssociationEditorMemberInput;
export type EditorPayout = AssociationEditorPayoutInput;

export interface MemberSharesChange { clientKey: string; shareCount: number; }
export interface ScheduleChange { index: number; value: EditorPayout; }

export function createClientKey(): string {
  return globalThis.crypto?.randomUUID?.() ?? `member-${Date.now()}-${Math.random().toString(16).slice(2)}`;
}
