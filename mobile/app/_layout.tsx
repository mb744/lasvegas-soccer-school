import '../src/i18n';
import React, { useEffect } from 'react';
import { Stack, useRouter } from 'expo-router';
import { GestureHandlerRootView } from 'react-native-gesture-handler';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { StatusBar } from 'expo-status-bar';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Notifications from 'expo-notifications';
import { AuthProvider } from '../src/auth/AuthContext';

const queryClient = new QueryClient({
  defaultOptions: { queries: { retry: 1, staleTime: 30_000 } },
});

/** Routes a tapped push notification to the right screen using its data payload. */
function NotificationRouter() {
  const router = useRouter();
  useEffect(() => {
    const sub = Notifications.addNotificationResponseReceivedListener((response) => {
      const data = response.notification.request.content.data as
        | { type?: string; groupId?: number; eventId?: number }
        | undefined;
      if (data?.type === 'chat' && data.groupId) {
        router.push(`/chat/${data.groupId}`);
      } else if (data?.type === 'event') {
        router.push('/(tabs)/schedule');
      }
    });
    return () => sub.remove();
  }, [router]);
  return null;
}

export default function RootLayout() {
  return (
    <GestureHandlerRootView style={{ flex: 1 }}>
      <SafeAreaProvider>
        <QueryClientProvider client={queryClient}>
          <AuthProvider>
            <StatusBar style="light" />
            <NotificationRouter />
            <Stack screenOptions={{ headerShown: false }}>
              <Stack.Screen name="index" />
              <Stack.Screen name="login" />
              <Stack.Screen name="(tabs)" />
              <Stack.Screen name="chat/[groupId]" options={{ headerShown: true, title: '' }} />
              <Stack.Screen name="invoices/index" options={{ headerShown: true, title: '' }} />
              <Stack.Screen name="invoices/[id]" options={{ headerShown: true, title: '' }} />
              <Stack.Screen name="events/[id]" options={{ headerShown: true, title: '' }} />
              <Stack.Screen name="admin/announcements/index" options={{ headerShown: true, title: '' }} />
              <Stack.Screen name="admin/announcements/[id]" options={{ headerShown: true, title: '' }} />
              <Stack.Screen name="admin/teams/index" options={{ headerShown: true, title: '' }} />
              <Stack.Screen name="admin/teams/[id]/index" options={{ headerShown: true, title: '' }} />
              <Stack.Screen name="admin/teams/[id]/coaches/[coachId]" options={{ headerShown: true, title: '' }} />
              <Stack.Screen name="admin/events/index" options={{ headerShown: true, title: '' }} />
              <Stack.Screen name="admin/events/[id]" options={{ headerShown: true, title: '' }} />
              <Stack.Screen name="admin/chat-groups/index" options={{ headerShown: true, title: '' }} />
              <Stack.Screen name="admin/chat-groups/[id]" options={{ headerShown: true, title: '' }} />
            </Stack>
          </AuthProvider>
        </QueryClientProvider>
      </SafeAreaProvider>
    </GestureHandlerRootView>
  );
}
