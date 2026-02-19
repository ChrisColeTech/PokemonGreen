import { Routes, Route } from 'react-router-dom';
import { Header } from './components/layout/Header';
import { Sidebar } from './components/layout/Sidebar';
import { ExportPage } from './pages/ExportPage';
import { ViewerPage } from './pages/ViewerPage';

export function App() {
    return (
        <>
            <Header />
            <div className="flex flex-1 min-h-0">
                <Sidebar />
                <main className="flex-1 min-w-0 min-h-0 overflow-y-auto bg-bg">
                    <Routes>
                        <Route path="/" element={<ViewerPage />} />
                        <Route path="/export" element={<ExportPage />} />
                    </Routes>
                </main>
            </div>
        </>
    );
}
