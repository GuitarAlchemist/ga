// src/components/PrimeRadiant/AuthBadge.tsx
// HUD chip showing auth state — "Sign in" button when anonymous, user
// avatar + menu when signed in. Sits next to the Admin chip.

import { useState } from 'react';
import { useAuth } from './AuthContext';
import { LoginModal } from './LoginModal';

export function AuthBadge(): JSX.Element {
  const { user, isAuthenticated, logout } = useAuth();
  const [loginOpen, setLoginOpen] = useState(false);
  const [menuOpen, setMenuOpen] = useState(false);

  if (!isAuthenticated) {
    return (
      <>
        <button
          className="prime-radiant__auth-signin"
          onClick={() => setLoginOpen(true)}
          title="Sign in with Google or GitHub"
        >
          Sign in
        </button>
        <LoginModal open={loginOpen} onClose={() => setLoginOpen(false)} />
      </>
    );
  }

  return (
    <>
      <button
        className="prime-radiant__auth-user"
        onClick={() => setMenuOpen((v) => !v)}
        title={`${user?.name ?? ''} · ${user?.email ?? ''}`}
      >
        {user?.avatarUrl ? (
          <img
            className="prime-radiant__auth-user-avatar"
            src={user.avatarUrl}
            alt=""
            referrerPolicy="no-referrer"
          />
        ) : (
          <span className="prime-radiant__auth-user-initials">
            {(user?.name ?? user?.email ?? '?').slice(0, 1).toUpperCase()}
          </span>
        )}
        <span className="prime-radiant__auth-user-name">
          {user?.name?.split(' ')[0] ?? 'User'}
        </span>
      </button>
      {menuOpen && (
        <div
          className="prime-radiant__auth-menu"
          onClick={(e) => e.stopPropagation()}
        >
          <div className="prime-radiant__auth-menu-header">
            <div className="prime-radiant__auth-menu-name">{user?.name}</div>
            <div className="prime-radiant__auth-menu-email">{user?.email}</div>
            <div className="prime-radiant__auth-menu-meta">
              {user?.provider} · {user?.roles?.join(', ') || 'viewer'}
            </div>
          </div>
          <button
            className="prime-radiant__auth-menu-item"
            onClick={() => { setMenuOpen(false); void logout(); }}
          >
            Sign out
          </button>
        </div>
      )}
    </>
  );
}
