import React from 'react';
import {
  ActivityIndicator,
  FlatList,
  RefreshControl,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
} from 'react-native';
import { Stack, useRouter } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { useQuery } from '@tanstack/react-query';
import { fetchInvoices } from '../../src/api/endpoints';
import { InvoiceStatus, type InvoiceSummary } from '../../src/api/types';
import { dueDateLabel, money } from '../../src/format';
import { colors, radius, spacing } from '../../src/theme';

export default function InvoicesListScreen() {
  const { t } = useTranslation();
  const router = useRouter();
  const { data, isLoading, isRefetching, refetch } = useQuery({
    queryKey: ['invoices'],
    queryFn: fetchInvoices,
  });

  if (isLoading) {
    return (
      <View style={styles.center}>
        <Stack.Screen options={{ title: t('invoices.title') }} />
        <ActivityIndicator size="large" color={colors.brand} />
      </View>
    );
  }

  return (
    <View style={styles.container}>
      <Stack.Screen options={{ title: t('invoices.title') }} />
      <FlatList
        data={data ?? []}
        keyExtractor={(i) => String(i.id)}
        contentContainerStyle={{ padding: spacing.lg }}
        refreshControl={<RefreshControl refreshing={isRefetching} onRefresh={refetch} tintColor={colors.brand} />}
        ListEmptyComponent={<Text style={styles.empty}>{t('invoices.empty')}</Text>}
        renderItem={({ item }) => (
          <InvoiceRow invoice={item} onPress={() => router.push(`/invoices/${item.id}`)} />
        )}
      />
    </View>
  );
}

function InvoiceRow({ invoice, onPress }: { invoice: InvoiceSummary; onPress: () => void }) {
  const { t } = useTranslation();
  const outstanding = invoice.status === InvoiceStatus.New || invoice.status === InvoiceStatus.Sent;
  const statusText = statusLabel(invoice.status, t);

  return (
    <TouchableOpacity style={[styles.row, outstanding && styles.rowOutstanding]} onPress={onPress} activeOpacity={0.85}>
      <View style={styles.rowLeft}>
        <Text style={styles.rowDescription} numberOfLines={2}>
          {invoice.description}
        </Text>
        {invoice.playerName ? <Text style={styles.rowMeta}>{invoice.playerName}</Text> : null}
        <Text style={styles.rowMeta}>
          {invoice.dueDate ? t('invoices.due', { date: dueDateLabel(invoice.dueDate) }) : t('invoices.noDueDate')}
        </Text>
      </View>
      <View style={styles.rowRight}>
        <Text style={[styles.rowAmount, outstanding && styles.rowAmountOutstanding]}>
          {money(invoice.amount, invoice.currency)}
        </Text>
        <View style={[styles.statusPill, pillStyle(invoice.status)]}>
          <Text style={styles.statusPillText}>{statusText}</Text>
        </View>
      </View>
    </TouchableOpacity>
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
  empty: { textAlign: 'center', color: colors.subtext, marginTop: spacing.xl, fontSize: 15 },
  row: {
    backgroundColor: colors.card,
    borderRadius: radius.lg,
    padding: spacing.lg,
    borderWidth: 1,
    borderColor: colors.border,
    marginBottom: spacing.sm,
    flexDirection: 'row',
    alignItems: 'flex-start',
  },
  rowOutstanding: { borderColor: colors.brand },
  rowLeft: { flex: 1, paddingRight: spacing.md },
  rowRight: { alignItems: 'flex-end' },
  rowDescription: { fontSize: 15, fontWeight: '700', color: colors.text, marginBottom: 4 },
  rowMeta: { fontSize: 13, color: colors.subtext, marginTop: 2 },
  rowAmount: { fontSize: 18, fontWeight: '800', color: colors.text },
  rowAmountOutstanding: { color: colors.brand },
  statusPill: {
    marginTop: spacing.xs,
    paddingHorizontal: spacing.sm,
    paddingVertical: 2,
    borderRadius: radius.sm,
  },
  statusPillText: { color: colors.white, fontSize: 11, fontWeight: '800', textTransform: 'uppercase' },
});
