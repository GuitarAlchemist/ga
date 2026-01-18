import { Component, OnInit, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { GaApiService } from '../services/ga-api.service';
import { BreadcrumbService } from '../services/navigation.service';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';

import { FretboardComponent } from '../shared/fretboard.component';
import { ProgressionComponent } from '../shared/progression.component';

@Component({
  selector: 'app-notebook-viewer',
  standalone: true,
  imports: [CommonModule, RouterLink, FretboardComponent, ProgressionComponent],
  template: `
    <div class="notebook-container" [class.dark]="isDark">
      <header class="page-header">
        <div class="header-main">
          <h1>📓 Notebook Viewer</h1>
        </div>
        <div class="header-actions">
            <span class="status-badge">.NET Interactive</span>
            <button class="theme-toggle" (click)="toggleTheme()">{{ isDark ? '☀️' : '🌙' }}</button>
        </div>
      </header>

      <div *ngIf="isLoading" class="loading-state">
        <div class="spinner"></div>
        <p>Loading notebook...</p>
      </div>

      <div *ngIf="!isLoading && notebook" class="notebook-content">
        <h1>{{ notebookName }}</h1>
        
        <div *ngFor="let cell of notebook.cells" class="cell" [class.code]="cell.cell_type === 'code'">
          <!-- Markdown Cell -->
          <div *ngIf="cell.cell_type === 'markdown'" class="markdown-cell" [innerHTML]="sanitizeHtml(renderMarkdown(cell.source))"></div>
          
          <!-- Code Cell -->
          <div *ngIf="cell.cell_type === 'code'" class="code-cell">
            <div class="cell-header">
                <span class="lang-badge">C# / F#</span>
                <button class="run-btn" (click)="executeCell(cell)" [disabled]="isExecuting && cell.img_executing">
                    {{ cell.img_executing ? '⏳' : '▶' }} Run
                </button>
            </div>
            <pre class="source-code"><code>{{ cell.source.join('') }}</code></pre>
            
            <!-- Cell Outputs -->
            <div *ngFor="let output of cell.outputs" class="output-area">
              <!-- Text Output -->
              <pre *ngIf="output.output_type === 'stream'" class="stream-output">{{ output.text.join('') }}</pre>
              
              
              <!-- HTML/Rich Output -->
              <div *ngIf="output.data && output.data['text/html']" 
                   class="html-output" 
                   [innerHTML]="sanitizeHtml(output.data['text/html'].join(''))"></div>

              <!-- Fretboard Output (Custom) -->
              <app-fretboard *ngIf="output.data && output.data['application/vnd.ga.fretboard+json']"
                             [frets]="output.data['application/vnd.ga.fretboard+json'].frets"
                             [title]="output.data['application/vnd.ga.fretboard+json'].name || 'Voicing'">
              </app-fretboard>

              <!-- Progression Output (Custom) -->
              <app-progression *ngIf="output.data && output.data['application/vnd.ga.progression+json']"
                               [events]="output.data['application/vnd.ga.progression+json']">
              </app-progression>
              
              <!-- Image Output -->
              <img *ngIf="output.data && output.data['image/png']"  
                   [src]="'data:image/png;base64,' + output.data['image/png']" 
                   class="image-output" />
            </div>
          </div>
        </div>
      </div>

      <div *ngIf="!isLoading && !notebook" class="error-state">
        <p>⚠️ Notebook not found or failed to load.</p>
        <a routerLink="/" class="back-link">Return to Dashboard</a>
      </div>
    </div>
  `,
  styles: [`
    :host { display: block; min-height: 100vh; }
    .notebook-container { padding: 0; font-family: 'Inter', system-ui, sans-serif; background: #f8f9fa; color: #333; min-height: 100vh; transition: all 0.3s; }
    .notebook-container.dark { background: #1a1a2e; color: #e0e0e0; }
    
    .page-header { display: flex; justify-content: space-between; align-items: center; padding: 16px 24px; border-bottom: 1px solid #eee; background: #fff; }
    .dark .page-header { background: #161b22; border-color: #333; }
    
    .header-main { display: flex; align-items: center; gap: 16px; }
    .header-main h1 { margin: 0; font-size: 1.1rem; font-weight: 600; }
    
    .header-actions { display: flex; align-items: center; gap: 12px; }
    .status-badge { font-size: 0.75rem; padding: 4px 8px; border-radius: 4px; background: #e3f2fd; color: #1565c0; }
    .dark .status-badge { background: #0d47a1; color: #e3f2fd; }

    .theme-toggle { background: none; border: 1px solid #ddd; border-radius: 6px; padding: 4px 8px; cursor: pointer; color: inherit; }
    .dark .theme-toggle { border-color: #444; }
    
    .notebook-content { padding: 24px; max-width: 1200px; margin: 0 auto; }
    
    .cell { margin-bottom: 24px; }
    .code-cell { border: 1px solid #eee; background: #fff; border-radius: 6px; overflow: hidden; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }
    .dark .code-cell { border-color: #333; background: #161b22; }
    
    .cell-header { display: flex; justify-content: space-between; align-items: center; padding: 4px 12px; background: #f8f9fa; border-bottom: 1px solid #eee; }
    .dark .cell-header { background: #0d1117; border-color: #333; }
    .lang-badge { font-size: 0.7rem; color: #666; font-family: monospace; }
    .run-btn { background: none; border: none; cursor: pointer; color: #28a745; font-size: 0.9rem; padding: 4px; display: flex; align-items: center; gap: 4px; }
    .run-btn:hover { color: #218838; }
    .run-btn:disabled { color: #ccc; cursor: not-allowed; }

    .source-code { padding: 12px; margin: 0; font-family: 'Fira Code', monospace; font-size: 0.9rem; overflow-x: auto; background: transparent; border: none; }
    .dark .source-code { color: #e6edf3; }
    
    .output-area { padding: 12px; font-size: 0.85rem; border-top: 1px solid #eee; background: #fafafa; }
    .dark .output-area { border-color: #333; background: #0d1117; }
    .stream-output { color: #333; margin: 0; white-space: pre-wrap; font-family: monospace; }
    .dark .stream-output { color: #e6edf3; }
    
    .loading-state { text-align: center; margin-top: 100px; }
  `]
})
export class NotebookViewerComponent implements OnInit {
  private readonly navService = inject(BreadcrumbService);
  private readonly gaApi = inject(GaApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly cdr = inject(ChangeDetectorRef);

  isDark = true;
  isLoading = true;
  notebook: any = null;
  notebookName = '';
  isExecuting = false;

  ngOnInit() {
    const path = this.route.snapshot.paramMap.get('path');
    if (path) {
      this.notebookName = path.split('/').pop()?.replace('.ipynb', '') || 'Notebook';
      this.updateBreadcrumbs();

      this.gaApi.getNotebook(path).subscribe({
        next: (data) => {
          this.notebook = data;
          this.isLoading = false;
          // Ensure structure for execution results locally
          if (this.notebook?.cells) {
            this.notebook.cells.forEach((c: any) => {
              if (!c.outputs) c.outputs = [];
            });
          }
          this.cdr.detectChanges();
        },
        error: (err) => {
          console.error('Failed to load notebook', err);
          this.isLoading = false;
        }
      });
    }
  }

  toggleTheme() {
    this.isDark = !this.isDark;
  }

  executeCell(cell: any) {
    if (cell.cell_type !== 'code') return;

    this.isExecuting = true;
    cell.img_executing = true; // Local state for spinner

    const code = cell.source.join('');

    this.gaApi.executeCode(code).subscribe({
      next: (result: any) => {
        console.log('Execution result:', result);
        cell.outputs = []; // Clear previous

        if (result.output) {
          cell.outputs.push({ output_type: 'stream', text: [result.output] });
        }

        if (result.error) {
          cell.outputs.push({ output_type: 'stream', text: [result.error], isError: true });
        }

        if (result.results) {
          result.results.forEach((r: any) => {
            // Detect custom Fretboard MIME
            if (r.mime === 'application/vnd.ga.fretboard+json') {
              cell.outputs.push({ data: { 'application/vnd.ga.fretboard+json': r.value } });
            } else if (r.mime === 'application/vnd.ga.progression+json') {
              cell.outputs.push({ data: { 'application/vnd.ga.progression+json': r.value } });
            } else if (r.mime === 'text/html') {
              cell.outputs.push({ data: { 'text/html': [r.value] } });
            } else {
              // Allow object display
              cell.outputs.push({ output_type: 'stream', text: [JSON.stringify(r, null, 2)] });
            }
          });
        }

        this.isExecuting = false;
        cell.img_executing = false;
        this.cdr.detectChanges();
      },
      error: (err: any) => {
        console.error('Execution failed', err);
        cell.outputs = [{ output_type: 'stream', text: [`Execution failed: ${err.message}`] }];
        this.isExecuting = false;
        cell.img_executing = false;
        this.cdr.detectChanges();
      }
    });
  }

  sanitizeHtml(html: string): SafeHtml {
    return this.sanitizer.bypassSecurityTrustHtml(html);
  }

  renderMarkdown(source: string[]): string {
    return source.join('')
      .replace(/^# (.*$)/gim, '<h1>$1</h1>')
      .replace(/^## (.*$)/gim, '<h2>$1</h2>')
      .replace(/^### (.*$)/gim, '<h3>$1</h3>')
      .replace(/\*\*(.*)\*\*/gim, '<strong>$1</strong>')
      .replace(/\*(.*)\*/gim, '<em>$1</em>');
  }

  private updateBreadcrumbs() {
    this.navService.setCrumbs([
      { label: 'Dashboard', path: '/' },
      { label: 'Notebooks', path: '/' },
      { label: this.notebookName }
    ]);
  }
}
