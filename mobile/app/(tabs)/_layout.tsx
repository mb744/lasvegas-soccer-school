import React, { useEffect } from 'react';
import { Text, type ColorValue } from 'react-native';
import { Redirect, Tabs } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../src/auth/AuthContext';
import { can, Perm } from '../../src/auth/can';
import { startChat, stopChat } from '../../src/chat/signalr';
import { colors } from '../../src/theme';

/** Simple emoji tab icons keep us dependency-free (no icon font to bundle/configure for v1).
 *  `color` is `ColorValue` (not just `string`) because RN 0.86's Tabs tabBarIcon callback types
 *  it as ColorValue — either a plain color string or a platform-opaque handle. */
function TabIcon({ icon, color }: { icon: string; color: ColorValue }) {
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
        name="index"
        options={{
          title: t('tabs.home'),
          tabBarIcon: ({ color }) => <TabIcon icon="🏠" color={color} />,
        }}
      />
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
          // View-only family members (grandparents, friends) aren't in team chats.
          href: me?.familyRole === 'viewer' ? null : undefined,
          title: t('tabs.chat'),
          tabBarIcon: ({ color }) => <TabIcon icon="💬" color={color} />,
        }}
      />
      <Tabs.Screen
        name="admin"
        options={{
          // href=null keeps the tab off the tab bar entirely for non-admins; expo-router still
          // resolves the route file so navigation from any admin-triggered link keeps working.
          href: can(me, Perm.AdminAccess, Perm.EventsCreate) ? undefined : null,
          title: t('tabs.admin'),
          tabBarIcon: ({ color }) => <TabIcon icon="🛠️" color={color} />,
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
