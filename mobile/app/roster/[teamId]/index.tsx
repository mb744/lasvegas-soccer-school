import React from 'react';
import { Stack, useLocalSearchParams } from 'expo-router';
import { useQueryClient } from '@tanstack/react-query';
import type { RosterTeam } from '../../../src/api/types';
import { TeamRoster } from '../../../src/roster/TeamRoster';

/** A team's roster, opened from the Roster tab's team list. */
export default function TeamRosterScreen() {
  const { teamId: raw } = useLocalSearchParams<{ teamId: string }>();
  const teamId = Number(raw);
  const qc = useQueryClient();
  const title = qc.getQueryData<RosterTeam[]>(['rosterTeams'])?.find((t) => t.id === teamId)?.name ?? '';

  return (
    <>
      <Stack.Screen options={{ title }} />
      <TeamRoster teamId={teamId} />
    </>
  );
}
