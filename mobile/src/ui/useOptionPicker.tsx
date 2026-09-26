import React from 'react';
import {
  ActionSheetIOS,
  FlatList,
  Modal,
  Platform,
  Pressable,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
} from 'react-native';
import { useTranslation } from 'react-i18next';
import { colors, radius, spacing } from '../theme';

export interface PickerOption {
  label: string;
  onPick: () => void;
}

/**
 * Pick one of a list of options. iOS uses the native action sheet; Android gets a scrollable modal
 * list (Android's Alert shows at most three buttons, so it can't list teams or venues). Render
 * `sheet` once in the screen.
 */
export function useOptionPicker() {
  const { t } = useTranslation();
  const [state, setState] = React.useState<{ title: string; options: PickerOption[] } | null>(null);

  const open = React.useCallback((title: string, options: PickerOption[]) => {
    if (Platform.OS === 'ios') {
      const labels = [...options.map((o) => o.label), t('common.cancel')];
      ActionSheetIOS.showActionSheetWithOptions(
        { options: labels, cancelButtonIndex: labels.length - 1, title },
        (idx) => { if (idx < options.length) options[idx].onPick(); },
      );
    } else {
      setState({ title, options });
    }
  }, [t]);

  const close = () => setState(null);

  const sheet = (
    <Modal visible={!!state} transparent animationType="fade" onRequestClose={close}>
      <Pressable style={styles.backdrop} onPress={close}>
        <Pressable style={styles.card} onPress={() => { /* keep taps inside the card */ }}>
          <Text style={styles.title}>{state?.title}</Text>
          <FlatList
            data={state?.options ?? []}
            keyExtractor={(_, i) => String(i)}
            renderItem={({ item }) => (
              <TouchableOpacity
                style={styles.row}
                onPress={() => { close(); item.onPick(); }}
              >
                <Text style={styles.rowText}>{item.label}</Text>
              </TouchableOpacity>
            )}
          />
          <TouchableOpacity style={styles.cancel} onPress={close}>
            <Text style={styles.cancelText}>{t('common.cancel')}</Text>
          </TouchableOpacity>
        </Pressable>
      </Pressable>
    </Modal>
  );

  return { open, sheet };
}

const styles = StyleSheet.create({
  backdrop: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.4)',
    justifyContent: 'center',
    padding: spacing.lg,
  },
  card: {
    backgroundColor: colors.card,
    borderRadius: radius.lg,
    paddingVertical: spacing.md,
    maxHeight: '80%',
  },
  title: {
    fontSize: 16,
    fontWeight: '800',
    color: colors.text,
    paddingHorizontal: spacing.lg,
    paddingBottom: spacing.sm,
  },
  row: {
    paddingHorizontal: spacing.lg,
    paddingVertical: spacing.md,
    borderTopWidth: StyleSheet.hairlineWidth,
    borderTopColor: colors.border,
  },
  rowText: { fontSize: 15, color: colors.text },
  cancel: {
    paddingHorizontal: spacing.lg,
    paddingTop: spacing.md,
    borderTopWidth: StyleSheet.hairlineWidth,
    borderTopColor: colors.border,
    alignItems: 'center',
  },
  cancelText: { fontSize: 15, fontWeight: '700', color: colors.brand },
});
