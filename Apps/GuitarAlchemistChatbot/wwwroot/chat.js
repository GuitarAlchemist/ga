// Simple helpers for the Guitar Alchemist chat UI
window.gaChat = {
    scrollToBottom: function (elementId) {
        try {
            var el = document.getElementById(elementId);
            if (!el) return;
            // Use requestAnimationFrame to ensure layout is ready
            requestAnimationFrame(function () {
                el.scrollTop = el.scrollHeight;
            });
        } catch (e) {
            console.warn('scrollToBottom failed', e);
        }
    },
    copyText: function (text) {
        if (!text) text = '';
        try {
            navigator.clipboard.writeText(text);
        } catch (e) {
            // Fallback
            var ta = document.createElement('textarea');
            ta.value = text;
            document.body.appendChild(ta);
            ta.select();
            try {
                document.execCommand('copy');
            } catch {
            }
            document.body.removeChild(ta);
        }
    }
};
// Simple helpers for the Guitar Alchemist chat UI
window.gaChat = {
    scrollToBottom: function (elementId) {
        try {
            var el = document.getElementById(elementId);
            if (!el) return;
            // Use requestAnimationFrame to ensure layout is ready
            requestAnimationFrame(function () {
                el.scrollTop = el.scrollHeight;
            });
        } catch (e) {
            console.warn('scrollToBottom failed', e);
        }
    },
    copyText: function (text) {
        if (!text) text = '';
        try {
            navigator.clipboard.writeText(text);
        } catch (e) {
            // Fallback
            var ta = document.createElement('textarea');
            ta.value = text;
            document.body.appendChild(ta);
            ta.select();
            try {
                document.execCommand('copy');
            } catch {
            }
            document.body.removeChild(ta);
        }
    }
};

// VexTab rendering removed - library was not loading reliably from CDN
// Future: Consider alternative music notation libraries or local hosting
