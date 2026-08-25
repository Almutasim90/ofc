import { BrowserRouter, Route, Routes } from 'react-router-dom'
import { lazy, Suspense } from 'react'
import { AuthProvider } from './auth/AuthContext'
import { ThemeProvider } from './theme/ThemeContext'
import ProtectedRoute from './auth/ProtectedRoute'
import HomeRedirect from './auth/HomeRedirect'
import Layout from './components/Layout'
import LoginPage from './pages/LoginPage'
import CashierPage from './pages/CashierPage'
import UsersPage from './pages/UsersPage'
import UserPermissionsPage from './pages/UserPermissionsPage'
import BranchesPage from './pages/BranchesPage'
import ProductsPage from './pages/ProductsPage'
import ProductRecipePage from './pages/ProductRecipePage'
import RawMaterialsPage from './pages/RawMaterialsPage'
import InventoryPage from './pages/InventoryPage'
import ShiftPage from './pages/ShiftPage'
import ClosingSchedulePage from './pages/ClosingSchedulePage'
import SettingsPage from './pages/SettingsPage'
import ChannelsPage from './pages/ChannelsPage'
import AiSettingsPage from './pages/AiSettingsPage'
import NotificationsPage from './pages/NotificationsPage'
import EmailSettingsPage from './pages/EmailSettingsPage'

const ReportsPage = lazy(() => import('./pages/ReportsPage'))

function App() {
  return (
    <ThemeProvider>
      <AuthProvider>
        <BrowserRouter>
          <Layout>
            <Routes>
              <Route path="/login" element={<LoginPage />} />
              <Route
                path="/cashier"
                element={
                  <ProtectedRoute permission="sales.create">
                    <CashierPage />
                  </ProtectedRoute>
                }
              />
              <Route
                path="/shift"
                element={<ProtectedRoute permission="sales.create"><ShiftPage /></ProtectedRoute>}
              />
              <Route path="/closing-schedule" element={
                <ProtectedRoute permission="closing.configure"><ClosingSchedulePage /></ProtectedRoute>
              } />
              <Route path="/reports" element={
                <ProtectedRoute permission="reports.branch.view">
                  <Suspense fallback={null}><ReportsPage /></Suspense>
                </ProtectedRoute>
              } />
              <Route path="/settings" element={<ProtectedRoute><SettingsPage /></ProtectedRoute>} />
              <Route path="/channels" element={<ProtectedRoute permission="channels.manage"><ChannelsPage /></ProtectedRoute>} />
              <Route path="/ai-settings" element={<ProtectedRoute permission="ai.manage"><AiSettingsPage /></ProtectedRoute>} />
              <Route path="/email-settings" element={<ProtectedRoute permission="email.manage"><EmailSettingsPage /></ProtectedRoute>} />
              <Route path="/notifications" element={<ProtectedRoute permission="inventory.adjust"><NotificationsPage /></ProtectedRoute>} />
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
              <Route
                path="/branches"
                element={
                  <ProtectedRoute permission="branches.manage">
                    <BranchesPage />
                  </ProtectedRoute>
                }
              />
              <Route
                path="/products"
                element={
                  <ProtectedRoute permission="products.manage">
                    <ProductsPage />
                  </ProtectedRoute>
                }
              />
              <Route
                path="/products/:id/recipe"
                element={
                  <ProtectedRoute permission="products.manage">
                    <ProductRecipePage />
                  </ProtectedRoute>
                }
              />
              <Route
                path="/raw-materials"
                element={
                  <ProtectedRoute permission="products.manage">
                    <RawMaterialsPage />
                  </ProtectedRoute>
                }
              />
              <Route
                path="/inventory"
                element={
                  <ProtectedRoute permission="inventory.adjust">
                    <InventoryPage />
                  </ProtectedRoute>
                }
              />
              <Route path="*" element={<HomeRedirect />} />
            </Routes>
          </Layout>
        </BrowserRouter>
      </AuthProvider>
    </ThemeProvider>
  )
}

export default App
