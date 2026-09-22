/** Date/time helpers for schedule rendering. Uses the device locale + timezone (Las Vegas parents
 * are on Pacific, matching the club), so no manual timezone math is needed on the client. */

export function dayKey(iso: string): string {
  const d = new Date(iso);
  return d.toLocaleDateString(undefined, { weekday: 'long', month: 'long', day: 'numeric' });
}

export function timeLabel(iso: string): string {
  const d = new Date(iso);
  return d.toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' });
}

export function shortDateTime(iso: string): string {
  const d = new Date(iso);
  return d.toLocaleString(undefined, {
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  });
}

export function longDate(iso: string): string {
  const d = new Date(iso);
  return d.toLocaleDateString(undefined, { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' });
}

/** Formats a DateOnly (YYYY-MM-DD) or ISO date without any timezone shifting into a short label. */
export function dueDateLabel(dateStr: string): string {
  const parts = dateStr.split('T')[0].split('-');
  if (parts.length !== 3) return dateStr;
  const [y, m, d] = parts.map(Number);
  const local = new Date(y, m - 1, d);
  return local.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' });
}

export function money(amount: number, currency = 'USD'): string {
  try {
    return new Intl.NumberFormat(undefined, { style: 'currency', currency, minimumFractionDigits: 2 }).format(amount);
  } catch {
    return `$${amount.toFixed(2)}`;
  }
}

export function messageTime(iso: string): string {
  const d = new Date(iso);
  const today = new Date();
  const sameDay = d.toDateString() === today.toDateString();
  return sameDay
    ? d.toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' })
    : d.toLocaleString(undefined, { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' });
}
