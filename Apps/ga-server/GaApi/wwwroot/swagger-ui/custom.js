// Guitar Alchemist API Documentation Custom JavaScript

(function () {
    'use strict';

    // Wait for Swagger UI to load
    function waitForSwaggerUI(callback) {
        if (window.ui && window.ui.getSystem) {
            callback();
        } else {
            setTimeout(() => waitForSwaggerUI(callback), 100);
        }
    }

    // Initialize custom functionality
    waitForSwaggerUI(function () {
        console.log('🎸 Guitar Alchemist API Documentation loaded');

        // Add custom functionality
        addQuickNavigation();
        addExampleButtons();
        addKeyboardShortcuts();
        addAnalytics();
        enhanceResponseDisplay();
        addCopyButtons();
        addFavorites();
    });

    // Add quick navigation menu
    function addQuickNavigation() {
        const nav = document.createElement('div');
        nav.className = 'ga-quick-nav';
        nav.innerHTML = `
            <div class="ga-nav-header">Quick Navigation</div>
            <div class="ga-nav-items">
                <a href="#tag/Musical-Knowledge" class="ga-nav-item">🎼 Musical Knowledge</a>
                <a href="#tag/Chord-Progressions" class="ga-nav-item">🎵 Chord Progressions</a>
                <a href="#tag/Guitar-Techniques" class="ga-nav-item">🎸 Guitar Techniques</a>
                <a href="#tag/Specialized-Tunings" class="ga-nav-item">🎛️ Specialized Tunings</a>
                <a href="#tag/Analytics" class="ga-nav-item">📊 Analytics</a>
                <a href="#tag/User-Personalization" class="ga-nav-item">👤 User Personalization</a>
            </div>
        `;

        // Add CSS for navigation
        const style = document.createElement('style');
        style.textContent = `
            .ga-quick-nav {
                position: fixed;
                top: 100px;
                right: 20px;
                background: white;
                border-radius: 8px;
                box-shadow: 0 4px 12px rgba(0,0,0,0.15);
                padding: 15px;
                z-index: 1000;
                max-width: 250px;
                border: 1px solid #e1e8ed;
            }
            .ga-nav-header {
                font-weight: bold;
                color: #2c3e50;
                margin-bottom: 10px;
                border-bottom: 2px solid #3498db;
                padding-bottom: 5px;
            }
            .ga-nav-item {
                display: block;
                padding: 8px 12px;
                color: #2c3e50;
                text-decoration: none;
                border-radius: 4px;
                margin: 2px 0;
                transition: all 0.3s ease;
            }
            .ga-nav-item:hover {
                background: #3498db;
                color: white;
                transform: translateX(5px);
            }
            @media (max-width: 1200px) {
                .ga-quick-nav { display: none; }
            }
        `;
        document.head.appendChild(style);
        document.body.appendChild(nav);
    }

    // Add example buttons for common queries
    function addExampleButtons() {
        const examples = [
            {label: 'Search Jazz', endpoint: '/api/MusicalKnowledge/search', params: {query: 'jazz'}},
            {label: 'Get Statistics', endpoint: '/api/MusicalKnowledge/statistics', params: {}},
            {label: 'Jazz Progressions', endpoint: '/api/ChordProgressions/category/Jazz', params: {}},
            {label: 'Guitar Techniques', endpoint: '/api/GuitarTechniques', params: {}}
        ];

        const exampleContainer = document.createElement('div');
        exampleContainer.className = 'ga-examples';
        exampleContainer.innerHTML = `
            <div class="ga-examples-header">Try These Examples</div>
            <div class="ga-examples-buttons">
                ${examples.map(ex => `
                    <button class="ga-example-btn" data-endpoint="${ex.endpoint}" data-params='${JSON.stringify(ex.params)}'>
                        ${ex.label}
                    </button>
                `).join('')}
            </div>
        `;

        // Add CSS for examples
        const style = document.createElement('style');
        style.textContent = `
            .ga-examples {
                background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
                color: white;
                padding: 20px;
                margin: 20px 0;
                border-radius: 8px;
                text-align: center;
            }
            .ga-examples-header {
                font-size: 18px;
                font-weight: bold;
                margin-bottom: 15px;
            }
            .ga-examples-buttons {
                display: flex;
                flex-wrap: wrap;
                gap: 10px;
                justify-content: center;
            }
            .ga-example-btn {
                background: rgba(255,255,255,0.2);
                color: white;
                border: 1px solid rgba(255,255,255,0.3);
                padding: 8px 16px;
                border-radius: 20px;
                cursor: pointer;
                transition: all 0.3s ease;
                font-weight: bold;
            }
            .ga-example-btn:hover {
                background: rgba(255,255,255,0.3);
                transform: translateY(-2px);
            }
        `;
        document.head.appendChild(style);

        // Insert after info section
        setTimeout(() => {
            const infoSection = document.querySelector('.swagger-ui .info');
            if (infoSection) {
                infoSection.parentNode.insertBefore(exampleContainer, infoSection.nextSibling);
            }
        }, 1000);

        // Add click handlers
        exampleContainer.addEventListener('click', (e) => {
            if (e.target.classList.contains('ga-example-btn')) {
                const endpoint = e.target.dataset.endpoint;
                const params = JSON.parse(e.target.dataset.params);
                executeExample(endpoint, params);
            }
        });
    }

    // Execute example API call
    function executeExample(endpoint, params) {
        // Find the corresponding operation in Swagger UI
        const operations = document.querySelectorAll('.opblock');
        for (const op of operations) {
            const pathElement = op.querySelector('.opblock-summary-path');
            if (pathElement && pathElement.textContent.includes(endpoint)) {
                // Expand the operation
                const summary = op.querySelector('.opblock-summary');
                if (summary && !op.classList.contains('is-open')) {
                    summary.click();
                }

                // Fill in parameters
                setTimeout(() => {
                    fillParameters(op, params);
                    // Click try it out
                    const tryItBtn = op.querySelector('.try-out__btn');
                    if (tryItBtn && tryItBtn.textContent === 'Try it out') {
                        tryItBtn.click();

                        // Execute after a short delay
                        setTimeout(() => {
                            const executeBtn = op.querySelector('.btn.execute');
                            if (executeBtn) {
                                executeBtn.click();
                            }
                        }, 500);
                    }
                }, 500);
                break;
            }
        }
    }

    // Fill parameters in operation
    function fillParameters(operation, params) {
        Object.keys(params).forEach(paramName => {
            const input = operation.querySelector(`input[placeholder*="${paramName}"], input[data-param-name="${paramName}"]`);
            if (input) {
                input.value = params[paramName];
                input.dispatchEvent(new Event('change', {bubbles: true}));
            }
        });
    }

    // Add keyboard shortcuts
    function addKeyboardShortcuts() {
        document.addEventListener('keydown', (e) => {
            // Ctrl/Cmd + K: Focus search
            if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
                e.preventDefault();
                const searchInput = document.querySelector('.swagger-ui .filter input');
                if (searchInput) {
                    searchInput.focus();
                }
            }

            // Escape: Close all expanded operations
            if (e.key === 'Escape') {
                const openOperations = document.querySelectorAll('.opblock.is-open .opblock-summary');
                openOperations.forEach(op => op.click());
            }
        });

        // Add keyboard shortcut hints
        const hints = document.createElement('div');
        hints.className = 'ga-keyboard-hints';
        hints.innerHTML = `
            <div class="ga-hints-header">Keyboard Shortcuts</div>
            <div class="ga-hint">Ctrl/Cmd + K: Focus search</div>
            <div class="ga-hint">Escape: Close all operations</div>
        `;

        const style = document.createElement('style');
        style.textContent = `
            .ga-keyboard-hints {
                position: fixed;
                bottom: 20px;
                right: 20px;
                background: rgba(44, 62, 80, 0.9);
                color: white;
                padding: 15px;
                border-radius: 8px;
                font-size: 12px;
                z-index: 1000;
                max-width: 200px;
            }
            .ga-hints-header {
                font-weight: bold;
                margin-bottom: 8px;
                border-bottom: 1px solid rgba(255,255,255,0.3);
                padding-bottom: 5px;
            }
            .ga-hint {
                margin: 4px 0;
                opacity: 0.8;
            }
            @media (max-width: 768px) {
                .ga-keyboard-hints { display: none; }
            }
        `;
        document.head.appendChild(style);
        document.body.appendChild(hints);
    }

    // Add analytics tracking
    function addAnalytics() {
        // Track API endpoint interactions
        document.addEventListener('click', (e) => {
            if (e.target.classList.contains('btn') && e.target.textContent === 'Execute') {
                const operation = e.target.closest('.opblock');
                const method = operation.querySelector('.opblock-summary-method')?.textContent;
                const path = operation.querySelector('.opblock-summary-path')?.textContent;

                console.log('API Call:', {method, path, timestamp: new Date().toISOString()});

                // You could send this to your analytics service
                // analytics.track('api_call_executed', { method, path });
            }
        });
    }

    // Enhance response display
    function enhanceResponseDisplay() {
        // Add response time tracking
        const originalFetch = window.fetch;
        window.fetch = function (...args) {
            const startTime = performance.now();
            return originalFetch.apply(this, args).then(response => {
                const endTime = performance.now();
                const responseTime = Math.round(endTime - startTime);

                // Add response time to UI
                setTimeout(() => {
                    const responseSection = document.querySelector('.responses-wrapper .live-responses-table');
                    if (responseSection) {
                        const timeElement = document.createElement('div');
                        timeElement.className = 'ga-response-time';
                        timeElement.innerHTML = `⏱️ Response time: ${responseTime}ms`;
                        timeElement.style.cssText = `
                            color: #27ae60;
                            font-weight: bold;
                            margin: 10px 0;
                            padding: 5px 10px;
                            background: rgba(39, 174, 96, 0.1);
                            border-radius: 4px;
                            border-left: 3px solid #27ae60;
                        `;
                        responseSection.insertBefore(timeElement, responseSection.firstChild);
                    }
                }, 100);

                return response;
            });
        };
    }

    // Add copy buttons to code blocks
    function addCopyButtons() {
        const observer = new MutationObserver((mutations) => {
            mutations.forEach((mutation) => {
                mutation.addedNodes.forEach((node) => {
                    if (node.nodeType === 1 && node.querySelector) {
                        const codeBlocks = node.querySelectorAll('.highlight-code, .microlight');
                        codeBlocks.forEach(addCopyButton);
                    }
                });
            });
        });

        observer.observe(document.body, {childList: true, subtree: true});

        function addCopyButton(codeBlock) {
            if (codeBlock.querySelector('.ga-copy-btn')) return;

            const copyBtn = document.createElement('button');
            copyBtn.className = 'ga-copy-btn';
            copyBtn.innerHTML = '📋 Copy';
            copyBtn.style.cssText = `
                position: absolute;
                top: 10px;
                right: 10px;
                background: #3498db;
                color: white;
                border: none;
                padding: 5px 10px;
                border-radius: 4px;
                cursor: pointer;
                font-size: 12px;
                z-index: 10;
            `;

            codeBlock.style.position = 'relative';
            codeBlock.appendChild(copyBtn);

            copyBtn.addEventListener('click', () => {
                const text = codeBlock.textContent;
                navigator.clipboard.writeText(text).then(() => {
                    copyBtn.innerHTML = '✅ Copied!';
                    setTimeout(() => {
                        copyBtn.innerHTML = '📋 Copy';
                    }, 2000);
                });
            });
        }
    }

    // Add favorites functionality
    function addFavorites() {
        const favorites = JSON.parse(localStorage.getItem('ga-api-favorites') || '[]');

        // Add favorite buttons to operations
        const observer = new MutationObserver((mutations) => {
            mutations.forEach((mutation) => {
                mutation.addedNodes.forEach((node) => {
                    if (node.nodeType === 1 && node.classList && node.classList.contains('opblock')) {
                        addFavoriteButton(node);
                    }
                });
            });
        });

        observer.observe(document.body, {childList: true, subtree: true});

        function addFavoriteButton(operation) {
            if (operation.querySelector('.ga-favorite-btn')) return;

            const summary = operation.querySelector('.opblock-summary');
            const path = operation.querySelector('.opblock-summary-path')?.textContent;
            const method = operation.querySelector('.opblock-summary-method')?.textContent;

            if (!summary || !path || !method) return;

            const isFavorite = favorites.some(fav => fav.path === path && fav.method === method);

            const favoriteBtn = document.createElement('button');
            favoriteBtn.className = 'ga-favorite-btn';
            favoriteBtn.innerHTML = isFavorite ? '⭐' : '☆';
            favoriteBtn.title = isFavorite ? 'Remove from favorites' : 'Add to favorites';
            favoriteBtn.style.cssText = `
                background: none;
                border: none;
                font-size: 18px;
                cursor: pointer;
                margin-left: 10px;
                color: #f39c12;
            `;

            summary.appendChild(favoriteBtn);

            favoriteBtn.addEventListener('click', (e) => {
                e.stopPropagation();
                toggleFavorite(path, method, favoriteBtn);
            });
        }

        function toggleFavorite(path, method, button) {
            const favorites = JSON.parse(localStorage.getItem('ga-api-favorites') || '[]');
            const index = favorites.findIndex(fav => fav.path === path && fav.method === method);

            if (index > -1) {
                favorites.splice(index, 1);
                button.innerHTML = '☆';
                button.title = 'Add to favorites';
            } else {
                favorites.push({path, method, timestamp: Date.now()});
                button.innerHTML = '⭐';
                button.title = 'Remove from favorites';
            }

            localStorage.setItem('ga-api-favorites', JSON.stringify(favorites));
        }
    }

})();
