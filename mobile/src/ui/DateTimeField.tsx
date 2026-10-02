import React from 'react';
import { Modal, Pressable, ScrollView, StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { colors, radius, spacing } from '../theme';

/**
 * Date + time field with a calendar and quick time choices. Pure JS (no native module), so it
 * ships in an over-the-air update. The value is local wall-clock time as "YYYY-MM-DDTHH:MM"
 * ('' = empty), the same string the event editor already saves.
 */
export function DateTimeField({
  value,
  onChange,
  title,
  placeholder,
  clearable = false,
  seed,
}: {
  value: string;
  onChange: (v: string) => void;
  /** Heading on the picker sheet. */
  title: string;
  placeholder: string;
  /** Shows "Clear" for optional fields (end, arrive). */
  clearable?: boolean;
  /** Where the picker starts when the field is empty, e.g. start + 90 min for the end time. */
  seed?: () => string;
}) {
  const { t, i18n } = useTranslation();
  const locale = i18n.language?.startsWith('es') ? 'es-US' : 'en-US';
  const insets = useSafeAreaInsets();
  const [open, setOpen] = React.useState(false);
  const [draft, setDraft] = React.useState<Parts>(() => parse(value) ?? defaultParts());
  const [view, setView] = React.useState({ y: draft.y, m: draft.m });

  const show = () => {
    const start = parse(value) ?? parse(seed?.() ?? '') ?? defaultParts();
    setDraft(start);
    setView({ y: start.y, m: start.m });
    setOpen(true);
  };
  const done = () => {
    onChange(format(draft));
    setOpen(false);
  };

  const shown = parse(value);
  const label = shown ? describe(shown, locale) : placeholder;

  // Calendar for the month on screen: blanks before the 1st, then each day.
  const firstWeekday = new Date(view.y, view.m, 1).getDay();
  const daysInMonth = new Date(view.y, view.m + 1, 0).getDate();
  const cells: (number | null)[] = [
    ...Array.from({ length: firstWeekday }, () => null),
    ...Array.from({ length: daysInMonth }, (_, i) => i + 1),
  ];
  while (cells.length % 7 !== 0) cells.push(null);
  const weekdays = Array.from({ length: 7 }, (_, i) =>
    new Date(2026, 1, i + 1).toLocaleDateString(locale, { weekday: 'narrow' })); // Feb 1 2026 is a Sunday
  const monthTitle = new Date(view.y, view.m, 1).toLocaleDateString(locale, { month: 'long', year: 'numeric' });
  const today = new Date();
  const isToday = (d: number) => d === today.getDate() && view.m === today.getMonth() && view.y === today.getFullYear();
  const isPicked = (d: number) => d === draft.d && view.m === draft.m && view.y === draft.y;

  const shiftMonth = (by: number) => {
    const next = new Date(view.y, view.m + by, 1);
    setView({ y: next.getFullYear(), m: next.getMonth() });
  };

  const hour12 = draft.h % 12 === 0 ? 12 : draft.h % 12;
  const pm = draft.h >= 12;
  const setHour12 = (h: number) => setDraft({ ...draft, h: (h % 12) + (pm ? 12 : 0) });
  const setPm = (isPm: boolean) => setDraft({ ...draft, h: (draft.h % 12) + (isPm ? 12 : 0) });
  // 5-minute steps; a time from elsewhere (e.g. 5:52 from a game sync) keeps its minute until changed.
  const minutes = Array.from({ length: 12 }, (_, i) => i * 5);

  return (
    <>
      <TouchableOpacity style={styles.field} onPress={show} accessibilityRole="button" accessibilityLabel={title}>
        <Text style={[styles.fieldText, !shown && styles.placeholder]}>{label}</Text>
        <Text style={styles.icon}>📅</Text>
      </TouchableOpacity>

      <Modal visible={open} transparent animationType="slide" onRequestClose={() => setOpen(false)}>
        <Pressable style={styles.backdrop} onPress={() => setOpen(false)} />
        <View style={[styles.sheet, { paddingBottom: spacing.lg + insets.bottom }]}>
          <ScrollView bounces={false}>
            <Text style={styles.title}>{title}</Text>
            <Text style={styles.preview}>{describe(draft, locale)}</Text>

            <View style={styles.monthRow}>
              <TouchableOpacity onPress={() => shiftMonth(-1)} style={styles.navBtn} accessibilityLabel={t('datePicker.prevMonth')}>
                <Text style={styles.navText}>‹</Text>
              </TouchableOpacity>
              <Text style={styles.monthTitle}>{monthTitle}</Text>
              <TouchableOpacity onPress={() => shiftMonth(1)} style={styles.navBtn} accessibilityLabel={t('datePicker.nextMonth')}>
                <Text style={styles.navText}>›</Text>
              </TouchableOpacity>
            </View>
            <View style={styles.grid}>
              {weekdays.map((w, i) => (
                <Text key={`w${i}`} style={styles.weekday}>{w}</Text>
              ))}
              {cells.map((d, i) => (
                <View key={i} style={styles.cell}>
                  {d ? (
                    <TouchableOpacity
                      style={[styles.day, isToday(d) && styles.dayToday, isPicked(d) && styles.dayPicked]}
                      onPress={() => setDraft({ ...draft, y: view.y, m: view.m, d })}
                    >
                      <Text style={[styles.dayText, isPicked(d) && styles.dayTextPicked]}>{d}</Text>
                    </TouchableOpacity>
                  ) : null}
                </View>
              ))}
            </View>

            <Text style={styles.section}>{t('datePicker.hour')}</Text>
            <View style={styles.chips}>
              {Array.from({ length: 12 }, (_, i) => i + 1).map((h) => (
                <Chip key={h} label={String(h)} active={h === hour12} onPress={() => setHour12(h)} />
              ))}
            </View>
            <Text style={styles.section}>{t('datePicker.minute')}</Text>
            <View style={styles.chips}>
              {minutes.map((mm) => (
                <Chip key={mm} label={`:${String(mm).padStart(2, '0')}`} active={mm === draft.min}
                  onPress={() => setDraft({ ...draft, min: mm })} />
              ))}
            </View>
            <View style={[styles.chips, { marginTop: spacing.sm }]}>
              <Chip label="AM" active={!pm} onPress={() => setPm(false)} wide />
              <Chip label="PM" active={pm} onPress={() => setPm(true)} wide />
            </View>

            <View style={styles.actions}>
              {clearable && value ? (
                <TouchableOpacity onPress={() => { onChange(''); setOpen(false); }} style={styles.secondaryBtn}>
                  <Text style={styles.clearText}>{t('datePicker.clear')}</Text>
                </TouchableOpacity>
              ) : null}
              <TouchableOpacity onPress={() => setOpen(false)} style={styles.secondaryBtn}>
                <Text style={styles.secondaryText}>{t('common.cancel')}</Text>
              </TouchableOpacity>
              <TouchableOpacity onPress={done} style={styles.primaryBtn}>
                <Text style={styles.primaryText}>{t('datePicker.done')}</Text>
              </TouchableOpacity>
            </View>
          </ScrollView>
        </View>
      </Modal>
    </>
  );
}

function Chip({ label, active, onPress, wide }: { label: string; active: boolean; onPress: () => void; wide?: boolean }) {
  return (
    <TouchableOpacity
      style={[styles.chip, wide && styles.chipWide, active && styles.chipActive]}
      onPress={onPress}
      accessibilityRole="button"
      accessibilityState={{ selected: active }}
    >
      <Text style={[styles.chipText, active && styles.chipTextActive]}>{label}</Text>
    </TouchableOpacity>
  );
}

type Parts = { y: number; m: number; d: number; h: number; min: number };

function parse(v: string): Parts | null {
  const m = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/.exec(v ?? '');
  if (!m) return null;
  return { y: +m[1], m: +m[2] - 1, d: +m[3], h: +m[4], min: +m[5] };
}

function format(p: Parts): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${p.y}-${pad(p.m + 1)}-${pad(p.d)}T${pad(p.h)}:${pad(p.min)}`;
}

/** Next whole hour, today. */
function defaultParts(): Parts {
  const d = new Date();
  d.setMinutes(0, 0, 0);
  d.setHours(d.getHours() + 1);
  return { y: d.getFullYear(), m: d.getMonth(), d: d.getDate(), h: d.getHours(), min: 0 };
}

function describe(p: Parts, locale: string): string {
  const d = new Date(p.y, p.m, p.d, p.h, p.min);
  const date = d.toLocaleDateString(locale, { weekday: 'short', month: 'short', day: 'numeric', year: 'numeric' });
  const time = d.toLocaleTimeString(locale, { hour: 'numeric', minute: '2-digit' });
  return `${date} · ${time}`;
}

/** "YYYY-MM-DDTHH:MM" shifted by some minutes (for seeding end / arrive from the start). */
export function shiftLocal(v: string, minutes: number): string {
  const p = parse(v);
  if (!p) return '';
  const d = new Date(p.y, p.m, p.d, p.h, p.min + minutes);
  return format({ y: d.getFullYear(), m: d.getMonth(), d: d.getDate(), h: d.getHours(), min: d.getMinutes() });
}

const styles = StyleSheet.create({
  field: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    backgroundColor: colors.card,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.md,
    padding: spacing.md,
  },
  fieldText: { fontSize: 15, color: colors.text, fontWeight: '600', flex: 1 },
  placeholder: { color: colors.subtext, fontWeight: '400' },
  icon: { fontSize: 16, marginLeft: spacing.sm },
  backdrop: { flex: 1, backgroundColor: 'rgba(0,0,0,0.35)' },
  sheet: {
    backgroundColor: colors.bg,
    borderTopLeftRadius: radius.lg,
    borderTopRightRadius: radius.lg,
    paddingHorizontal: spacing.lg,
    paddingTop: spacing.lg,
    maxHeight: '90%',
  },
  title: { fontSize: 13, fontWeight: '800', color: colors.subtext, textTransform: 'uppercase' },
  preview: { fontSize: 18, fontWeight: '800', color: colors.text, marginTop: spacing.xs, marginBottom: spacing.md },
  monthRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', marginBottom: spacing.sm },
  navBtn: { paddingHorizontal: spacing.md, paddingVertical: spacing.xs },
  navText: { fontSize: 26, color: colors.brand, fontWeight: '700' },
  monthTitle: { fontSize: 16, fontWeight: '800', color: colors.text, textTransform: 'capitalize' },
  grid: { flexDirection: 'row', flexWrap: 'wrap' },
  weekday: { width: `${100 / 7}%`, textAlign: 'center', fontSize: 12, fontWeight: '700', color: colors.subtext, paddingVertical: 4 },
  cell: { width: `${100 / 7}%`, aspectRatio: 1, padding: 2 },
  day: { flex: 1, alignItems: 'center', justifyContent: 'center', borderRadius: 999 },
  dayToday: { borderWidth: 1, borderColor: colors.brand },
  dayPicked: { backgroundColor: colors.brand },
  dayText: { fontSize: 15, color: colors.text, fontWeight: '600' },
  dayTextPicked: { color: colors.white, fontWeight: '800' },
  section: { fontSize: 12, fontWeight: '800', color: colors.subtext, textTransform: 'uppercase', marginTop: spacing.md, marginBottom: spacing.xs },
  chips: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.xs },
  chip: {
    minWidth: 46,
    paddingVertical: spacing.sm,
    paddingHorizontal: spacing.sm,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border,
    backgroundColor: colors.card,
    alignItems: 'center',
  },
  chipWide: { flex: 1 },
  chipActive: { backgroundColor: colors.brand, borderColor: colors.brand },
  chipText: { fontSize: 14, fontWeight: '700', color: colors.text },
  chipTextActive: { color: colors.white },
  actions: { flexDirection: 'row', justifyContent: 'flex-end', gap: spacing.sm, marginTop: spacing.lg },
  secondaryBtn: { paddingVertical: spacing.md, paddingHorizontal: spacing.lg },
  secondaryText: { fontSize: 15, fontWeight: '700', color: colors.subtext },
  clearText: { fontSize: 15, fontWeight: '700', color: colors.danger },
  primaryBtn: { backgroundColor: colors.brand, borderRadius: radius.md, paddingVertical: spacing.md, paddingHorizontal: spacing.xl },
  primaryText: { fontSize: 15, fontWeight: '800', color: colors.white },
});
