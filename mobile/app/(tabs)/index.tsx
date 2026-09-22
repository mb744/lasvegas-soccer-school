import React from 'react';
import {
  ActivityIndicator,
  RefreshControl,
  ScrollView,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
} from 'react-native';
import { useRouter } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { useQuery } from '@tanstack/react-query';
import { fetchInvoices } from '../../src/api/endpoints';
import { InvoiceStatus, type InvoiceSummary } from '../../src/api/types';
import { useAuth } from '../../src/auth/AuthContext';
import { dueDateLabel, money } from '../../src/format';
import { colors, radius, spacing } from '../../src/theme';

/**
 * Home landing screen. Greets the parent, then surfaces the single most-relevant outstanding
 * invoice (nearest due date wins) as a tap-through into the full invoices list. Kept intentionally
 * light — this is the first screen after sign-in and needs to render fast even on cold cache.
 */
export default function HomeScreen() {
  const { t } = useTranslation();
  const { me } = useAuth();
  const router = useRouter();

  const { data, isLoading, isRefetching, refetch } = useQuery({
    queryKey: ['invoices'],
    queryFn: fetchInvoices,
  });

  const outstanding = React.useMemo(
    () => (data ?? []).find((i) => i.status === InvoiceStatus.New || i.status === InvoiceStatus.Sent),
    [data],
  );

  const greeting = t('home.greeting', { name: me?.firstName || '' });

  return (
    <ScrollView
      style={styles.container}
      contentContainerStyle={{ padding: spacing.lg }}
      refreshControl={<RefreshControl refreshing={isRefetching} onRefresh={refetch} tintColor={colors.brand} />}
    >
      <Text style={styles.greeting}>{greeting}</Text>

      <Text style={styles.sectionTitle}>{t('home.outstandingTitle')}</Text>
      {isLoading ? (
        <View style={styles.loadingCard}>
          <ActivityIndicator color={colors.brand} />
        </View>
      ) : outstanding ? (
        <OutstandingInvoiceCard invoice={outstanding} onPress={() => router.push(`/invoices/${outstanding.id}`)} />
      ) : (
        <View style={styles.emptyCard}>
          <Text style={styles.emptyCardText}>{t('home.noOutstanding')}</Text>
        </View>
      )}

      <TouchableOpacity style={styles.viewAllBtn} onPress={() => router.push('/invoices')}>
        <Text style={styles.viewAllBtnText}>{t('home.viewAll')}</Text>
      </TouchableOpacity>
    </ScrollView>
  );
}

function OutstandingInvoiceCard({ invoice, onPress }: { invoice: InvoiceSummary; onPress: () => void }) {
  const { t } = useTranslation();
  return (
    <TouchableOpacity style={styles.invoiceCard} onPress={onPress} activeOpacity={0.8}>
      <View style={styles.invoiceCardHeader}>
        <Text style={styles.invoiceAmount}>{money(invoice.amount, invoice.currency)}</Text>
        <View style={styles.dueBadge}>
          <Text style={styles.dueBadgeText}>
            {invoice.dueDate ? t('invoices.due', { date: dueDateLabel(invoice.dueDate) }) : t('invoices.noDueDate')}
          </Text>
        </View>
      </View>
      <Text style={styles.invoiceDescription} numberOfLines={2}>
        {invoice.description}
      </Text>
      {invoice.playerName ? <Text style={styles.invoiceMeta}>{invoice.playerName}</Text> : null}
      <View style={styles.invoiceCta}>
        <Text style={styles.invoiceCtaText}>{t('home.seeInvoice')} →</Text>
      </View>
    </TouchableOpacity>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg },
  greeting: { fontSize: 24, fontWeight: '800', color: colors.text, marginBottom: spacing.lg },
  sectionTitle: {
    fontSize: 13,
    fontWeight: '800',
    color: colors.subtext,
    textTransform: 'uppercase',
    marginBottom: spacing.sm,
  },
  loadingCard: {
    backgroundColor: colors.card,
    borderRadius: radius.lg,
    padding: spacing.xl,
    borderWidth: 1,
    borderColor: colors.border,
    alignItems: 'center',
    marginBottom: spacing.lg,
  },
  emptyCard: {
    backgroundColor: colors.card,
    borderRadius: radius.lg,
    padding: spacing.xl,
    borderWidth: 1,
    borderColor: colors.border,
    alignItems: 'center',
    marginBottom: spacing.lg,
  },
  emptyCardText: { color: colors.subtext, fontSize: 15 },
  invoiceCard: {
    backgroundColor: colors.card,
    borderRadius: radius.lg,
    padding: spacing.lg,
    borderWidth: 1,
    borderColor: colors.brand,
    marginBottom: spacing.lg,
  },
  invoiceCardHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: spacing.sm,
  },
  invoiceAmount: { fontSize: 28, fontWeight: '800', color: colors.brand },
  dueBadge: {
    backgroundColor: colors.danger,
    borderRadius: radius.sm,
    paddingHorizontal: spacing.sm,
    paddingVertical: 4,
  },
  dueBadgeText: { color: colors.white, fontSize: 11, fontWeight: '800', textTransform: 'uppercase' },
  invoiceDescription: { fontSize: 16, fontWeight: '700', color: colors.text, marginBottom: spacing.xs },
  invoiceMeta: { fontSize: 14, color: colors.subtext, marginBottom: spacing.sm },
  invoiceCta: {
    marginTop: spacing.sm,
    paddingTop: spacing.sm,
    borderTopWidth: 1,
    borderTopColor: colors.border,
    alignItems: 'flex-end',
  },
  invoiceCtaText: { color: colors.brand, fontSize: 14, fontWeight: '800' },
  viewAllBtn: {
    backgroundColor: colors.brand,
    borderRadius: radius.md,
    paddingVertical: spacing.lg,
    alignItems: 'center',
  },
  viewAllBtnText: { color: colors.white, fontSize: 16, fontWeight: '800' },
});
