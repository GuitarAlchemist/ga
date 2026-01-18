import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { GaApiService } from '../services/ga-api.service';
import { BreadcrumbService } from '../services/navigation.service';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';

@Component({
    selector: 'app-documentation-viewer',
    standalone: true,
    imports: [CommonModule, RouterLink],
    template: `
    <div class="doc-container" [class.dark]="isDark">
      <header class="page-header">
        <div class="header-main">
          <h1>📚 Documentation Viewer</h1>
        </div>
        <button class="theme-toggle" (click)="toggleTheme()">{{ isDark ? '☀️' : '🌙' }}</button>
      </header>

      <div *ngIf="isLoading" class="loading-state">
        <div class="spinner"></div>
        <p>Loading documentation...</p>
      </div>

      <div *ngIf="!isLoading && content" class="doc-content">
        <h1>{{ docTitle }}</h1>
        <div class="markdown-body" [innerHTML]="sanitizeHtml(renderMarkdown(content))"></div>
      </div>

      <div *ngIf="!isLoading && !content" class="error-state">
        <p>⚠️ Documentation not found or failed to load.</p>
        <a routerLink="/" class="back-link">Return to Dashboard</a>
      </div>
    </div>
  `,
    styles: [`
    :host { display: block; min-height: 100vh; }
    .doc-container { padding: 16px 24px; font-family: 'Inter', system-ui, sans-serif; background: #f8f9fa; color: #333; min-height: 100vh; transition: all 0.3s; }
    .doc-container.dark { background: #1a1a2e; color: #e0e0e0; }
    
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px; }
    .header-main h1 { margin: 0; font-size: 1.2rem; }
    .theme-toggle { background: none; border: 1px solid #ddd; border-radius: 6px; padding: 4px 8px; cursor: pointer; color: inherit; }
    .dark .theme-toggle { border-color: #444; }
    
    .loading-state { display: flex; flex-direction: column; align-items: center; margin-top: 100px; }
    .spinner { width: 40px; height: 40px; border: 3px solid #ddd; border-top-color: #667eea; border-radius: 50%; animation: spin 1s linear infinite; margin-bottom: 12px; }
    @keyframes spin { to { transform: rotate(360deg); } }

    .doc-content h1 { font-size: 1.5rem; margin-bottom: 24px; border-bottom: 1px solid #eee; padding-bottom: 8px; }
    .dark .doc-content h1 { border-color: #333; }
    .markdown-body { line-height: 1.6; font-size: 0.95rem; }
    .markdown-body h1, .markdown-body h2, .markdown-body h3 { margin-top: 24px; margin-bottom: 12px; }
    .markdown-body code { background: #e9ecef; padding: 2px 4px; border-radius: 3px; font-size: 0.9em; }
    .dark .markdown-body code { background: #333; color: #aaa; }
    .markdown-body pre { background: #1e1e1e; color: #d4d4d4; padding: 12px; border-radius: 6px; overflow-x: auto; margin: 16px 0; }
    
    .error-state { text-align: center; margin-top: 60px; }
    .back-link { color: #667eea; text-decoration: none; }
    .dark .back-link { color: #6db3f2; }
  `]
})
export class DocumentationViewerComponent implements OnInit {
    private readonly navService = inject(BreadcrumbService);
    private readonly gaApi = inject(GaApiService);
    private readonly route = inject(ActivatedRoute);
    private readonly sanitizer = inject(DomSanitizer);

    isDark = true;
    isLoading = true;
    content: string = '';
    docTitle = '';

    ngOnInit() {
        const path = this.route.snapshot.paramMap.get('path');
        if (path) {
            this.docTitle = path.split('/').pop()?.replace('.md', '').replace(/_/g, ' ') || 'Documentation';
            this.updateBreadcrumbs();

            this.gaApi.getDocContent(path).subscribe({
                next: (data) => {
                    this.content = data;
                    this.isLoading = false;
                    this.updateBreadcrumbs();
                },
                error: (err) => {
                    console.error('Failed to load documentation', err);
                    this.isLoading = false;
                }
            });
        }
    }

    toggleTheme() {
        this.isDark = !this.isDark;
    }

    sanitizeHtml(html: string): SafeHtml {
        return this.sanitizer.bypassSecurityTrustHtml(html);
    }

    renderMarkdown(text: string): string {
        return text
            .replace(/^# (.*$)/gim, '<h1>$1</h1>')
            .replace(/^## (.*$)/gim, '<h2>$1</h2>')
            .replace(/^### (.*$)/gim, '<h3>$1</h3>')
            .replace(/\*\*(.*)\*\*/gim, '<strong>$1</strong>')
            .replace(/\*(.*)\*/gim, '<em>$1</em>')
            .replace(/```([\s\S]*?)```/gim, '<pre><code>$1</code></pre>')
            .replace(/`(.*?)`/gim, '<code>$1</code>');
    }

    private updateBreadcrumbs() {
        this.navService.setCrumbs([
            { label: 'Dashboard', path: '/' },
            { label: 'Documentation', path: '/' },
            { label: this.docTitle }
        ]);
    }
}
