import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AuthProvider } from './auth/AuthContext'
import ProtectedRoute from './auth/ProtectedRoute'
import Layout from './components/Layout'
import LoginPage from './pages/LoginPage'
import UsersPage from './pages/UsersPage'
import UserPermissionsPage from './pages/UserPermissionsPage'

function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Layout>
          <Routes>
            <Route path="/login" element={<LoginPage />} />
            <Route
              path="/users"
              element={
                <ProtectedRoute permission="users.manage">
                  <UsersPage />
                </ProtectedRoute>
              }
            />
            <Route
              path="/users/:id/permissions"
              element={
                <ProtectedRoute permission="users.manage">
                  <UserPermissionsPage />
                </ProtectedRoute>
              }
            />
            <Route path="*" element={<Navigate to="/users" replace />} />
          </Routes>
        </Layout>
      </BrowserRouter>
    </AuthProvider>
  )
}

export default App
