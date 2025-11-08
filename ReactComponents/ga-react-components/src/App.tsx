import React from 'react';
import { ThemeProvider } from '@mui/material/styles';
import CssBaseline from '@mui/material/CssBaseline';
import { useLocation, Link as RouterLink } from 'react-router-dom';
import { theme } from './theme';
import './AppLayout.css';

interface AppProps {
  children: React.ReactNode;
}

interface BreadcrumbBarProps {
  pathnames: string[];
}

const BreadcrumbBar: React.FC<BreadcrumbBarProps> = ({ pathnames }) => {
  if (pathnames.length === 0) {
    return null;
  }

  const segments = pathnames.map((segment, index) => {
    const to = `/${pathnames.slice(0, index + 1).join('/')}`;
    const label = segment
      .split('-')
      .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
      .join(' ');

    return { to, label, isLast: index === pathnames.length - 1 };
  });

  return (
    <div className="breadcrumbBar">
      <button
        type="button"
        className="backButton"
        onClick={() => window.open('http://localhost:5173/demos/all', '_blank')}
      >
        ⬅ Back
      </button>

      <nav aria-label="breadcrumb" className="breadcrumbs">
        <RouterLink to="/" className="breadcrumbLink">
          Home
        </RouterLink>
        {segments.map(({ to, label, isLast }) => (
          <span key={to} className="breadcrumbItem">
            <span className="breadcrumbSeparator">/</span>
            {isLast ? (
              <span className="breadcrumbLabel">{label}</span>
            ) : (
              <RouterLink to={to} className="breadcrumbLink">
                {label}
              </RouterLink>
            )}
          </span>
        ))}
      </nav>

      <span className="portIndicator">Demos (Port 5176)</span>
    </div>
  );
};

const App: React.FC<AppProps> = ({ children }) => {
  const location = useLocation();
  const pathnames = location.pathname.split('/').filter(Boolean);

  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <div className="rootLayout">
        <BreadcrumbBar pathnames={pathnames} />
        <div className="mainContent">{children}</div>
      </div>
    </ThemeProvider>
  );
};

export default App;
