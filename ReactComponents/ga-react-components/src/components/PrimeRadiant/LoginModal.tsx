// src/components/PrimeRadiant/LoginModal.tsx
// OAuth2 login modal — two buttons (Google, GitHub) that redirect to the
// backend's /api/auth/challenge/{provider} endpoint. The backend handles
// the OAuth dance and returns a JWT in the URL fragment on success.

import { useAuth } from './AuthContext';

export interface LoginModalProps {
  open: boolean;
  onClose: () => void;
  message?: string;
}

export function LoginModal({ open, onClose, message }: LoginModalProps): JSX.Element | null {
  const { login } = useAuth();
  if (!open) return null;

  return (
    <div
      className="prime-radiant__login-backdrop"
      onClick={onClose}
    >
      <div
        className="prime-radiant__login-modal"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="prime-radiant__login-header">
          <span className="prime-radiant__login-title">SIGN IN</span>
          <button
            className="prime-radiant__login-close"
            onClick={onClose}
            aria-label="Close"
          >✕</button>
        </div>
        {message && <div className="prime-radiant__login-message">{message}</div>}
        <div className="prime-radiant__login-providers">
          <button
            className="prime-radiant__login-provider prime-radiant__login-provider--google"
            onClick={() => login('google')}
          >
            <span className="prime-radiant__login-provider-icon">G</span>
            <span>Continue with Google</span>
          </button>
          <button
            className="prime-radiant__login-provider prime-radiant__login-provider--github"
            onClick={() => login('github')}
          >
            <svg
              viewBox="0 0 16 16"
              width="16" height="16"
              className="prime-radiant__login-provider-icon"
              aria-hidden="true"
            >
              <path fill="currentColor" d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z"/>
            </svg>
            <span>Continue with GitHub</span>
          </button>
        </div>
        <div className="prime-radiant__login-footer">
          We only see your email, name, and avatar. No writes to your account.
        </div>
      </div>
    </div>
  );
}
