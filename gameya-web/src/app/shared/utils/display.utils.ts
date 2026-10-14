export function formatMoney(value: number | null | undefined): string {
  const amount = Number.isFinite(value) ? Number(value) : 0;
  return `${new Intl.NumberFormat('ar-EG', { maximumFractionDigits: 2 }).format(amount)} جنيه`;
}

export function formatDate(value: string | Date | null | undefined, includeTime = false): string {
  if (!value) return '—';
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) return '—';
  return new Intl.DateTimeFormat('ar-EG', includeTime
    ? { dateStyle: 'medium', timeStyle: 'short' }
    : { day: 'numeric', month: 'long', year: 'numeric' }).format(date);
}

export function safePercent(part: number, total: number): number {
  if (total <= 0) return 0;
  return Math.min(100, Math.max(0, Math.round((part / total) * 100)));
}
