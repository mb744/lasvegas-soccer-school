import { BrowserRouter, Route, Routes } from 'react-router-dom'
import { LandingPage } from './pages/LandingPage'
import { RegisterPage } from './pages/RegisterPage'
import { AdminPage } from './pages/AdminPage'

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<LandingPage />} />
        <Route path="/register/:token" element={<RegisterPage />} />
        <Route path="/admin" element={<AdminPage />} />
        <Route path="*" element={<LandingPage />} />
      </Routes>
    </BrowserRouter>
  )
}
