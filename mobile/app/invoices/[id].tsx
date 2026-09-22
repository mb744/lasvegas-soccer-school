import React from 'react';
import { ActivityIndicator, ScrollView, StyleSheet, Text, View } from 'react-native';
import { Stack, useLocalSearchParams } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { useQuery } from '@tanstack/react-query';
import { fetchInvoice } from '../../src/api/endpoints';
import { InvoiceStatus } from '../../src/api/types';
import { dueDateLabel, longDate, money } from '../../src/format';
import { colors, radius, spacing } from '../../src/theme';

export default function InvoiceDetailScreen() {
  const { t } = useTranslation();
  const { id } = useLocalSearchParams<{ id: string }>();
  const invoiceId = Number(id);

  const { data, isLoading } = useQuery({
    queryKey: ['invoice', invoiceId],
    queryFn: () => fetchInvoice(invoiceId),
    enabled: Number.isFinite(invoiceId),
  });

  if (isLoading) {
    return (
      <View style={styles.center}>
        <Stack.Screen options={{ title: t('invoices.detailTitle') }} />
        <ActivityIndicator size="large" color={colors.brand} />
      </View>
    );
  }
  if (!data) return null;

  const outstanding = data.status === InvoiceStatus.New || data.status === InvoiceStatus.Sent;
  const paid = data.status === InvoiceStatus.Paid;

  return (
    <ScrollView style={styles.container} contentContainerStyle={{ padding: spacing.lg }}>
      <Stack.Screen options={{ title: t('invoices.detailTitle') }} />

      <View style={[styles.heroCard, outstanding && styles.heroCardOutstanding, paid && styles.heroCardPaid]}>
        <Text style={styles.amount}>{money(data.amount, data.currency)}</Text>
        <Text style={styles.description}>{data.description}</Text>
        <View style={[styles.statusPill, pillStyle(data.status)]}>
          <Text style={styles.statusPillText}>{statusLabel(data.status, t)}</Text>
        </View>
      </View>

      <View style={styles.card}>
        {data.playerName ? (
          <Row label={t('invoices.player')} value={data.playerName} />
        ) : null}
        {data.chargeTypeName ? (
          <Row label={t('invoices.chargeType')} value={data.chargeTypeName} />
        ) : null}
        <Row label={t('invoices.issuedOn')} value={longDate(data.issuedAt)} />
        <Row
          label={t('invoices.dueOn')}
          value={data.dueDate ? dueDateLabel(data.dueDate) : t('invoices.noDueDate')}
        />
        {data.paidAt ? <Row label={t('invoices.paidOn')} value={longDate(data.paidAt)} /> : null}
        {data.paymentMethod ? (
          <Row label={t('invoices.paymentMethod')} value={data.paymentMethod} />
        ) : null}
        {data.paymentReference ? (
          <Row label={t('invoices.paymentReference')} value={data.paymentReference} />
        ) : null}
      </View>

      {outstanding && (
        <View style={styles.payCard}>
          <Text style={styles.payTitle}>{t('invoices.howToPay')}</Text>
          <Text style={styles.paySubtitle}>{t('invoices.payInstructionsTitle')}</Text>
          <Text style={styles.payBody}>{t('invoices.payInstructions')}</Text>
        </View>
      )}
    </ScrollView>
  );
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <View style={styles.row}>
      <Text style={styles.rowLabel}>{label}</Text>
      <Text style={styles.rowValue}>{value}</Text>
    </View>
  );
}

function statusLabel(status: InvoiceStatus, t: (k: string) => string): string {
  switch (status) {
    case InvoiceStatus.New:
      return t('invoices.status.new');
    case InvoiceStatus.Sent:
      return t('invoices.status.sent');
    case InvoiceStatus.Paid:
      return t('invoices.status.paid');
    case InvoiceStatus.Closed:
      return t('invoices.status.closed');
  }
}

function pillStyle(status: InvoiceStatus) {
  if (status === InvoiceStatus.Paid) return { backgroundColor: colors.success };
  if (status === InvoiceStatus.Closed) return { backgroundColor: colors.subtext };
  return { backgroundColor: colors.danger };
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.bg },
  heroCard: {
    backgroundColor: colors.card,
    borderRadius: radius.lg,
    padding: spacing.xl,
    borderWidth: 1,
    borderColor: colors.border,
    marginBottom: spacing.lg,
    alignItems: 'center',
  },
  heroCardOutstanding: { borderColor: colors.brand, borderWidth: 2 },
  heroCardPaid: { borderColor: colors.success, borderWidth: 2 },
  amount: { fontSize: 40, fontWeight: '800', color: colors.text, marginBottom: spacing.sm },
  description: { fontSize: 17, fontWeight: '700', color: colors.text, textAlign: 'center', marginBottom: spacing.md },
  statusPill: { paddingHorizontal: spacing.md, paddingVertical: 4, borderRadius: 999 },
  statusPillText: { color: colors.white, fontSize: 12, fontWeight: '800', textTransform: 'uppercase' },
  card: {
    backgroundColor: colors.card,
    borderRadius: radius.lg,
    padding: spacing.lg,
    borderWidth: 1,
    borderColor: colors.border,
    marginBottom: spacing.lg,
  },
  row: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: spacing.sm,
    borderBottomWidth: 1,
    borderBottomColor: colors.border,
  },
  rowLabel: { fontSize: 13, fontWeight: '700', color: colors.subtext, textTransform: 'uppercase' },
  rowValue: { fontSize: 15, color: colors.text, flexShrink: 1, textAlign: 'right', marginLeft: spacing.md },
  payCard: {
    backgroundColor: colors.brand,
    borderRadius: radius.lg,
    padding: spacing.lg,
  },
  payTitle: { color: colors.white, fontSize: 20, fontWeight: '800', marginBottom: spacing.xs },
  paySubtitle: { color: colors.accent, fontSize: 13, fontWeight: '700', textTransform: 'uppercase', marginBottom: spacing.md },
  payBody: { color: colors.white, fontSize: 15, lineHeight: 22 },
});
