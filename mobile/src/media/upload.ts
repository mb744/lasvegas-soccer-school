import * as ImagePicker from 'expo-image-picker';
import * as FileSystem from 'expo-file-system/legacy';
import { completeMediaUpload, createMediaUpload } from '../api/endpoints';
import { MediaKind, type MediaItem } from '../api/types';

export const MAX_IMAGE_BYTES = 10 * 1024 * 1024;
export const MAX_VIDEO_BYTES = 100 * 1024 * 1024;
export const MAX_VIDEO_SECONDS = 60;

export type PickSource = 'camera' | 'library';

export interface PickedMedia {
  uri: string;
  kind: MediaKind;
  contentType: string;
  sizeBytes: number;
}

/** Thrown with an i18n key so screens can show a localized message. */
export class MediaError extends Error {
  constructor(public readonly key: string) {
    super(key);
  }
}

const EXT_TYPES: Record<string, string> = {
  jpg: 'image/jpeg', jpeg: 'image/jpeg', png: 'image/png', heic: 'image/heic', heif: 'image/heif',
  webp: 'image/webp', mp4: 'video/mp4', mov: 'video/quicktime', m4v: 'video/mp4',
};

/** Opens the camera or library. Returns null if the user cancels. */
export async function pickMedia(source: PickSource): Promise<PickedMedia | null> {
  const perm =
    source === 'camera'
      ? await ImagePicker.requestCameraPermissionsAsync()
      : await ImagePicker.requestMediaLibraryPermissionsAsync();
  if (!perm.granted) throw new MediaError('media.permissionDenied');

  const options: ImagePicker.ImagePickerOptions = {
    mediaTypes: ['images', 'videos'],
    videoMaxDuration: MAX_VIDEO_SECONDS,
    quality: 0.8,
    // Convert HEIC/HEVC to JPEG/H.264 so every phone (incl. Android) can display it.
    preferredAssetRepresentationMode: ImagePicker.UIImagePickerPreferredAssetRepresentationMode.Compatible,
  };
  const result =
    source === 'camera'
      ? await ImagePicker.launchCameraAsync(options)
      : await ImagePicker.launchImageLibraryAsync(options);
  if (result.canceled || !result.assets?.[0]) return null;

  const asset = result.assets[0];
  const kind = asset.type === 'video' ? MediaKind.Video : MediaKind.Image;
  const ext = asset.uri.split('.').pop()?.toLowerCase() ?? '';
  const contentType = (asset.mimeType ?? EXT_TYPES[ext] ?? (kind === MediaKind.Video ? 'video/mp4' : 'image/jpeg')).toLowerCase();

  let sizeBytes = asset.fileSize ?? 0;
  if (!sizeBytes) {
    const info = await FileSystem.getInfoAsync(asset.uri);
    sizeBytes = info.exists ? info.size : 0;
  }

  if (kind === MediaKind.Image && sizeBytes > MAX_IMAGE_BYTES) throw new MediaError('media.imageTooLarge');
  if (kind === MediaKind.Video) {
    if (sizeBytes > MAX_VIDEO_BYTES) throw new MediaError('media.videoTooLarge');
    // duration is in milliseconds; library picks aren't trimmed by videoMaxDuration on every OS.
    if ((asset.duration ?? 0) > (MAX_VIDEO_SECONDS + 1) * 1000) throw new MediaError('media.videoTooLong');
  }

  return { uri: asset.uri, kind, contentType, sizeBytes };
}

/**
 * Uploads straight to blob storage with the API-issued SAS URL, then asks the API to verify it.
 * `onProgress` receives 0..1.
 */
export async function uploadMedia(picked: PickedMedia, onProgress?: (fraction: number) => void): Promise<MediaItem> {
  const { mediaId, uploadUrl } = await createMediaUpload(picked.kind, picked.contentType, picked.sizeBytes);

  const task = FileSystem.createUploadTask(
    uploadUrl,
    picked.uri,
    {
      httpMethod: 'PUT',
      uploadType: FileSystem.FileSystemUploadType.BINARY_CONTENT,
      headers: { 'x-ms-blob-type': 'BlockBlob', 'Content-Type': picked.contentType },
    },
    (p) => {
      if (onProgress && p.totalBytesExpectedToSend > 0) onProgress(p.totalBytesSent / p.totalBytesExpectedToSend);
    },
  );
  const res = await task.uploadAsync();
  if (!res || res.status < 200 || res.status >= 300) throw new MediaError('media.uploadFailed');

  return completeMediaUpload(mediaId);
}
