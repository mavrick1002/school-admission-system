import { Route, Routes } from 'react-router-dom'
import { Layout } from './components/Layout'
import { ClassListPage } from './pages/ClassListPage'
import { CreateClassPage } from './pages/CreateClassPage'
import { HoldPage } from './pages/HoldPage'
import { MyHoldsPage } from './pages/MyHoldsPage'
import { NotFoundPage } from './pages/NotFoundPage'

export default function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route index element={<ClassListPage />} />
        <Route path="admin/classes/new" element={<CreateClassPage />} />
        <Route path="holds/:holdId" element={<HoldPage />} />
        <Route path="me" element={<MyHoldsPage />} />
        <Route path="*" element={<NotFoundPage />} />
      </Route>
    </Routes>
  )
}
