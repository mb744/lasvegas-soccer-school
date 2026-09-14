import React, { useEffect } from 'react';
import { Text } from 'react-native';
import { Redirect, Tabs } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../src/auth/AuthContext';
import { startChat, stopChat } from '../../src/chat/signalr';
import { colors } from '../../src/theme';

/** Simple emoji tab icons keep us dependency-free (no icon font to bundle/configure for v1). */
function TabIcon({ icon, color }: { icon: string; color: string }) {
  return <Text style={{ fontSize: 22, color }}>{icon}</Text>;
}

export default function TabsLayout() {
  const { me } = useAuth();
  const { t } = useTranslation();

  // Open the realtime chat connection while signed in; tear it down on sign-out.
  useEffect(() => {
    if (me) {
      void startChat();
      return () => {
        void stopChat();
      };
    }
  }, [me]);

  if (!me) return <Redirect href="/login" />;

  return (
    <Tabs
      screenOptions={{
        headerStyle: { backgroundColor: colors.brand },
        headerTintColor: colors.white,
        tabBarActiveTintColor: colors.brand,
        tabBarInactiveTintColor: colors.subtext,
      }}
    >
      <Tabs.Screen
        name="schedule"
        options={{
          title: t('tabs.schedule'),
          tabBarIcon: ({ color }) => <TabIcon icon="📅" color={color} />,
        }}
      />
      <Tabs.Screen
        name="chat"
        options={{
          title: t('tabs.chat'),
          tabBarIcon: ({ color }) => <TabIcon icon="💬" color={color} />,
        }}
      />
      <Tabs.Screen
        name="profile"
        options={{
          title: t('tabs.profile'),
          tabBarIcon: ({ color }) => <TabIcon icon="👤" color={color} />,
        }}
      />
    </Tabs>
  );
}
