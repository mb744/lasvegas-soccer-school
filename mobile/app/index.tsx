import React from 'react';
import { ActivityIndicator, View } from 'react-native';
import { Redirect } from 'expo-router';
import { useAuth } from '../src/auth/AuthContext';
import { colors } from '../src/theme';

/** Entry gate: wait for session restore, then send to the tabs or the login screen. */
export default function Index() {
  const { me, loading } = useAuth();

  if (loading) {
    return (
      <View style={{ flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.bg }}>
        <ActivityIndicator size="large" color={colors.brand} />
      </View>
    );
  }

  return <Redirect href={me ? '/(tabs)/schedule' : '/login'} />;
}
