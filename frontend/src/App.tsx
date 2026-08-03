import { BrowserRouter, Route, Routes } from 'react-router-dom'
import { LandingPage } from './pages/LandingPage'
import { RegisterPage } from './pages/RegisterPage'
import { AccountPage } from './pages/AccountPage'
import { AdminPage } from './pages/AdminPage'
import { AdminOutreachPage } from './pages/admin/OutreachPage'
import { AdminRegistrationsPage } from './pages/admin/RegistrationsPage'
import { AdminUsersPage } from './pages/admin/UsersPage'
import { AdminMessagingPage } from './pages/admin/MessagingPage'
import { AdminAgeClassificationsPage } from './pages/admin/AgeClassificationsPage'
import { AdminTeamsPage } from './pages/admin/TeamsPage'
import { AdminEventsPage } from './pages/admin/EventsPage'
import { AdminSettingsPage } from './pages/admin/SettingsPage'
import { AdminCoachesPage } from './pages/admin/CoachesPage'
import { AdminPlayersPage } from './pages/admin/PlayersPage'
import { AdminInvoicesPage } from './pages/admin/InvoicesPage'
import { AdminHostedTournamentsPage } from './pages/admin/HostedTournamentsPage'
import { AdminChatGroupsPage } from './pages/admin/ChatGroupsPage'
import { TournamentPublicPage } from './pages/TournamentPublicPage'
import { LoginPage } from './pages/LoginPage'
import { SignupPage } from './pages/SignupPage'
import { PrivacyPage } from './pages/PrivacyPage'
import { DataDeletionPage } from './pages/DataDeletionPage'
import { InfoPage } from './pages/InfoPage'
import { PricingPage } from './pages/PricingPage'
import { AboutPage } from './pages/AboutPage'
import { AuthProvider } from './auth/AuthContext'
import { RequireAuth } from './auth/RequireAuth'

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <Routes>
          <Route path="/" element={<LandingPage />} />
          <Route path="/login" element={<LoginPage />} />
          <Route path="/signup" element={<SignupPage />} />
          <Route path="/about" element={<AboutPage />} />
          <Route path="/info" element={<InfoPage />} />
          <Route path="/pricing" element={<PricingPage />} />
          <Route path="/privacy" element={<PrivacyPage />} />
          <Route path="/data-deletion" element={<DataDeletionPage />} />
          <Route
            path="/register"
            element={<RequireAuth><RegisterPage /></RequireAuth>}
          />
          <Route
            path="/account"
            element={<RequireAuth><AccountPage /></RequireAuth>}
          />
          <Route
            path="/admin"
            element={<RequireAuth adminOnly><AdminPage /></RequireAuth>}
          />
          <Route
            path="/admin/outreach"
            element={<RequireAuth adminOnly><AdminOutreachPage /></RequireAuth>}
          />
          <Route
            path="/admin/registrations"
            element={<RequireAuth adminOnly><AdminRegistrationsPage /></RequireAuth>}
          />
          <Route
            path="/admin/users"
            element={<RequireAuth adminOnly><AdminUsersPage /></RequireAuth>}
          />
          <Route
            path="/admin/messaging"
            element={<RequireAuth adminOnly><AdminMessagingPage /></RequireAuth>}
          />
          <Route
            path="/admin/age-classifications"
            element={<RequireAuth adminOnly><AdminAgeClassificationsPage /></RequireAuth>}
          />
          <Route
            path="/admin/teams"
            element={<RequireAuth adminOnly><AdminTeamsPage /></RequireAuth>}
          />
          <Route
            path="/admin/events"
            element={<RequireAuth adminOnly><AdminEventsPage /></RequireAuth>}
          />
          <Route
            path="/admin/settings"
            element={<RequireAuth adminOnly><AdminSettingsPage /></RequireAuth>}
          />
          <Route
            path="/admin/coaches"
            element={<RequireAuth adminOnly><AdminCoachesPage /></RequireAuth>}
          />
          <Route
            path="/admin/players"
            element={<RequireAuth adminOnly><AdminPlayersPage /></RequireAuth>}
          />
          <Route
            path="/admin/invoices"
            element={<RequireAuth adminOnly><AdminInvoicesPage /></RequireAuth>}
          />
          <Route
            path="/admin/hosted-tournaments"
            element={<RequireAuth adminOnly><AdminHostedTournamentsPage /></RequireAuth>}
          />
          <Route
            path="/admin/chat-groups"
            element={<RequireAuth adminOnly><AdminChatGroupsPage /></RequireAuth>}
          />
          <Route path="/tournament/:slug" element={<TournamentPublicPage />} />
          <Route path="*" element={<LandingPage />} />
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  )
}
