import React from 'react';
import {
  ActionSheetIOS,
  Alert,
  Image,
  Modal,
  Platform,
  Pressable,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
  type ViewStyle,
} from 'react-native';
import { useVideoPlayer, VideoView } from 'expo-video';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { MediaKind, type MediaItem } from '../api/types';
import { colors, radius, spacing } from '../theme';
import { MediaError, type PickSource } from './upload';

/** Camera vs library chooser. Resolves null on cancel. */
export function askMediaSource(t: TFunction): Promise<PickSource | null> {
  return new Promise((resolve) => {
    const cancel = t('common.cancel');
    const camera = t('media.takePhotoVideo');
    const library = t('media.chooseFromLibrary');
    if (Platform.OS === 'ios') {
      ActionSheetIOS.showActionSheetWithOptions(
        { options: [cancel, camera, library], cancelButtonIndex: 0 },
        (i) => resolve(i === 1 ? 'camera' : i === 2 ? 'library' : null),
      );
    } else {
      Alert.alert(
        t('media.addTitle'),
        '',
        [
          { text: cancel, style: 'cancel', onPress: () => resolve(null) },
          { text: camera, onPress: () => resolve('camera') },
          { text: library, onPress: () => resolve('library') },
        ],
        { cancelable: true, onDismiss: () => resolve(null) },
      );
    }
  });
}

export function alertMediaError(t: TFunction, e: unknown) {
  const key = e instanceof MediaError ? e.key : 'media.uploadFailed';
  Alert.alert(t('media.errorTitle'), t(key));
}

/** Square/rect preview. Videos show a play badge; tap opens the full-screen viewer. */
export function MediaThumb({
  media,
  style,
  onPress,
  onLongPress,
}: {
  media: MediaItem;
  style?: ViewStyle;
  onPress: () => void;
  onLongPress?: () => void;
}) {
  return (
    <TouchableOpacity activeOpacity={0.85} onPress={onPress} onLongPress={onLongPress} delayLongPress={350} style={[styles.thumb, style]}>
      {media.kind === MediaKind.Image ? (
        <Image source={{ uri: media.url }} style={StyleSheet.absoluteFill} resizeMode="cover" />
      ) : (
        <View style={[StyleSheet.absoluteFill, styles.videoThumb]}>
          <Text style={styles.play}>▶</Text>
        </View>
      )}
    </TouchableOpacity>
  );
}

export function MediaViewer({ media, onClose }: { media: MediaItem | null; onClose: () => void }) {
  const { t } = useTranslation();
  return (
    <Modal visible={!!media} animationType="fade" transparent={false} onRequestClose={onClose} supportedOrientations={['portrait', 'landscape']}>
      <View style={styles.viewer}>
        {media?.kind === MediaKind.Image ? (
          <Pressable style={StyleSheet.absoluteFill} onPress={onClose}>
            <Image source={{ uri: media.url }} style={StyleSheet.absoluteFill} resizeMode="contain" />
          </Pressable>
        ) : media ? (
          <VideoPlayerView url={media.url} />
        ) : null}
        <TouchableOpacity style={styles.close} onPress={onClose} accessibilityLabel={t('common.close')}>
          <Text style={styles.closeText}>✕</Text>
        </TouchableOpacity>
      </View>
    </Modal>
  );
}

function VideoPlayerView({ url }: { url: string }) {
  const player = useVideoPlayer(url, (p) => {
    p.play();
  });
  return <VideoView player={player} style={StyleSheet.absoluteFill} nativeControls contentFit="contain" />;
}

/** Thin progress bar shown while an upload is in flight. */
export function UploadProgress({ fraction, label }: { fraction: number; label: string }) {
  return (
    <View style={styles.progressWrap}>
      <Text style={styles.progressLabel}>
        {label} {Math.round(fraction * 100)}%
      </Text>
      <View style={styles.progressTrack}>
        <View style={[styles.progressFill, { width: `${Math.max(3, Math.round(fraction * 100))}%` }]} />
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  thumb: { overflow: 'hidden', borderRadius: radius.md, backgroundColor: colors.border },
  videoThumb: { backgroundColor: '#1b1b1b', alignItems: 'center', justifyContent: 'center' },
  play: { color: colors.white, fontSize: 28 },
  viewer: { flex: 1, backgroundColor: '#000' },
  close: {
    position: 'absolute',
    top: 54,
    right: spacing.lg,
    width: 40,
    height: 40,
    borderRadius: 20,
    backgroundColor: 'rgba(0,0,0,0.55)',
    alignItems: 'center',
    justifyContent: 'center',
  },
  closeText: { color: colors.white, fontSize: 20, fontWeight: '800' },
  progressWrap: { paddingHorizontal: spacing.md, paddingVertical: spacing.sm },
  progressLabel: { fontSize: 12, color: colors.subtext, marginBottom: 4 },
  progressTrack: { height: 4, borderRadius: 2, backgroundColor: colors.border, overflow: 'hidden' },
  progressFill: { height: 4, backgroundColor: colors.brand },
});
