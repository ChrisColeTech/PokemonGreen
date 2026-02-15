import { Header } from './components/layout/Header';
import { Sidebar } from './components/layout/Sidebar';
import { MainContent } from './components/layout/MainContent';
import { GalleryPanel } from './components/gallery/GalleryPanel';

export function App() {
  return (
    <div className="h-screen flex flex-col overflow-hidden select-none">
      <Header />
      <div className="flex-1 flex min-h-0">
        <Sidebar />
        <MainContent />
        <GalleryPanel />
      </div>
    </div>
  );
}
