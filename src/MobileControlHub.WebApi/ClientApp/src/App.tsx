import { BrowserRouter, Routes, Route, NavLink } from 'react-router-dom';
import { LayoutDashboard, Smartphone, Monitor, Server, ScrollText, Settings } from 'lucide-react';
import Dashboard from './pages/Dashboard';
import Devices from './pages/Devices';
import Sessions from './pages/Sessions';
import VpsRemote from './pages/VpsRemote';
import Logs from './pages/Logs';
import SettingsPage from './pages/Settings';

function App() {
  return (
    <BrowserRouter>
      <aside className="sidebar">
        <div className="sidebar-header">
          <h1>
            <Smartphone size={20} />
            <span>Mobile Control Hub</span>
          </h1>
          <p>Device Management Console</p>
        </div>
        <nav className="sidebar-nav">
          <NavLink to="/" end>
            <LayoutDashboard size={18} />
            <span>Dashboard</span>
          </NavLink>
          <NavLink to="/devices">
            <Smartphone size={18} />
            <span>Devices</span>
          </NavLink>
          <NavLink to="/sessions">
            <Monitor size={18} />
            <span>Sessions</span>
          </NavLink>
          <NavLink to="/vps">
            <Server size={18} />
            <span>VPS / Remote</span>
          </NavLink>
          <NavLink to="/logs">
            <ScrollText size={18} />
            <span>Logs</span>
          </NavLink>
          <NavLink to="/settings">
            <Settings size={18} />
            <span>Settings</span>
          </NavLink>
        </nav>
      </aside>
      <main className="main-content">
        <Routes>
          <Route path="/" element={<Dashboard />} />
          <Route path="/devices" element={<Devices />} />
          <Route path="/sessions" element={<Sessions />} />
          <Route path="/vps" element={<VpsRemote />} />
          <Route path="/logs" element={<Logs />} />
          <Route path="/settings" element={<SettingsPage />} />
        </Routes>
      </main>
    </BrowserRouter>
  );
}

export default App;
