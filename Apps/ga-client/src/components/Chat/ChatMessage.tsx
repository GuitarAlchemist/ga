import React, { useMemo } from "react";
import { Avatar } from "@mui/material";
import { Person, MusicNote, ContentCopy } from "@mui/icons-material";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import { Prism as SyntaxHighlighter } from "react-syntax-highlighter";
import { vscDarkPlus } from "react-syntax-highlighter/dist/esm/styles/prism";
import type { ChatMessage as ChatMessageType } from "../../store/chatAtoms";
import MemoizedVexTab from "./MemoizedVexTab";
import MemoizedCodeBlock from "./MemoizedCodeBlock";

interface ChatMessageProps {
  message: ChatMessageType;
}

const bubbleBaseStyle: React.CSSProperties = {
  padding: "12px 16px",
  boxShadow: "0 4px 12px rgba(0,0,0,0.25)",
  maxWidth: "640px",
  position: "relative",
};

const labelStyle: React.CSSProperties = {
  fontSize: "0.75rem",
  opacity: 0.7,
  marginBottom: 8,
  fontWeight: 600,
};

const footerStyle: React.CSSProperties = {
  fontSize: "0.7rem",
  opacity: 0.5,
  marginTop: 8,
  textAlign: "right",
};

const ChatMessage: React.FC<ChatMessageProps> = ({ message }) => {
  const isUser = message.role === "user";
  const isSystem = message.role === "system";

  const markdownContent = useMemo(
    () => (
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        components={{
          code({ inline, className, children, ...props }: any) {
            const match = /language-(\w+)/.exec(className || "");
            const isVextab = className?.includes("language-vextab");

            if (isVextab) {
              return <MemoizedVexTab content={String(children)} />;
            }

            if (!inline && match) {
              return (
                <MemoizedCodeBlock
                  language={match[1]}
                  content={String(children).replace(/\n$/, "")}
                />
              );
            }

            return (
              <code
                style={{
                  backgroundColor: "rgba(255,255,255,0.12)",
                  padding: "0 4px",
                  borderRadius: "4px",
                }}
                {...props}
              >
                {children}
              </code>
            );
          },
          table({ children }) {
            return (
              <div style={{ overflowX: "auto", margin: "8px 0" }}>
                <table style={{ width: "100%", borderCollapse: "collapse" }}>{children}</table>
              </div>
            );
          },
          th({ children }) {
            return (
              <th
                style={{
                  border: "1px solid rgba(255,255,255,0.16)",
                  padding: "8px",
                  textAlign: "left",
                }}
              >
                {children}
              </th>
            );
          },
          td({ children }) {
            return (
              <td
                style={{
                  border: "1px solid rgba(255,255,255,0.16)",
                  padding: "8px",
                  verticalAlign: "top",
                }}
              >
                {children}
              </td>
            );
          },
        }}
      >
        {message.content}
      </ReactMarkdown>
    ),
    [message.content],
  );

  const handleCopy = () => {
    navigator.clipboard.writeText(message.content).catch((error) => {
      console.error("Failed to copy message:", error);
    });
  };

  if (isSystem) {
    return (
      <div
        data-testid="chat-message"
        style={{ display: "flex", justifyContent: "center", margin: "16px 0" }}
      >
        <div
          style={{
            padding: "8px 16px",
            borderRadius: 12,
            border: "1px dashed rgba(255,255,255,0.2)",
            backgroundColor: "#1e2333",
            color: "rgba(255,255,255,0.7)",
            fontSize: "0.9rem",
          }}
        >
          {message.content}
        </div>
      </div>
    );
  }

  const bubbleStyle: React.CSSProperties = {
    ...bubbleBaseStyle,
    backgroundColor: isUser ? "#1f6feb" : "#1e2333",
    color: "#ffffff",
    borderRadius: isUser ? "16px 4px 16px 16px" : "4px 16px 16px 16px",
  };

  const containerStyle: React.CSSProperties = {
    display: 'flex',
    justifyContent: isUser ? 'flex-end' : 'flex-start',
    gap: '12px',
    marginBottom: '16px',
  };

  const copyButtonStyle: React.CSSProperties = {
    position: 'absolute',
    top: 8,
    right: 8,
    background: 'transparent',
    border: 0,
    color: 'inherit',
    cursor: 'pointer',
    opacity: 0.7,
  };

  return (
    <div data-testid="chat-message" style={containerStyle}>
      {!isUser && (
        <Avatar sx={{ bgcolor: 'primary.main', width: 32, height: 32 }}>
          <MusicNote />
        </Avatar>
      )}

      <div style={bubbleStyle}>
        {!isUser && (
          <button type="button" onClick={handleCopy} style={copyButtonStyle} aria-label="Copy message">
            <ContentCopy fontSize="small" />
          </button>
        )}

        <div style={labelStyle}>{isUser ? 'You' : 'Guitar Alchemist'}</div>
        <div style={{ whiteSpace: 'pre-wrap', wordBreak: 'break-word' }}>{markdownContent}</div>
        <div style={footerStyle}>
          {message.timestamp.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
        </div>
      </div>

      {isUser && (
        <Avatar sx={{ bgcolor: 'secondary.main', width: 32, height: 32 }}>
          <Person />
        </Avatar>
      )}
    </div>
  );
};

export default ChatMessage;
