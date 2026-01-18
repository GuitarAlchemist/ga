import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, ChangeDetectorRef } from '@angular/core';
import { RouterLink } from '@angular/router';
import { GaApiService } from '../services/ga-api.service';
import { BreadcrumbService } from '../services/navigation.service';

interface BenchmarkResult {
    id: string;
    name: string;
    score: number;
    timestamp: string;
    status: 'pass' | 'fail' | 'warn';
}

interface DocLink {
    title: string;
    path: string;
    description: string;
}

interface Notebook {
    name: string;
    path: string;
    lastModified: string;
    type?: string;
    tags?: string[];
}

@Component({
    selector: 'app-home',
    standalone: true,
    imports: [CommonModule, RouterLink],
    template: `
    <div class="dashboard" [class.dark]="isDark">
      <header class="page-header">
        <div class="header-main">
          <h1>🎸 Guitar Alchemist Dashboard</h1>
          <p class="subtitle">AI-Driven Music Information Retrieval & Analysis</p>
        </div>
        <div class="header-actions">
          <div class="instrument-info" *ngIf="instrument">
            <span class="tag">🎸 {{ instrument }}</span>
            <span class="tag">🎼 {{ tuning }}</span>
          </div>
          <button class="theme-toggle" (click)="toggleTheme()">{{ isDark ? '☀️' : '🌙' }}</button>
        </div>
      </header>

      <section class="metrics">
        <div class="metric-card">
          <span class="value">{{ voicingCount | number }}</span>
          <span class="label">Indexed Voicings</span>
        </div>
        <div class="metric-card">
          <span class="value">{{ embeddingDim }}</span>
          <span class="label">Embedding Dims</span>
        </div>
        <div class="metric-card" [class.pass]="avgScore >= 0.8" [class.warn]="avgScore < 0.8 && avgScore >= 0.6" [class.fail]="avgScore < 0.6">
          <span class="value">{{ (avgScore * 100).toFixed(0) }}%</span>
          <span class="label">Avg Quality Score</span>
        </div>
        <div class="metric-card">
          <span class="value">{{ searchLatencyMs }}ms</span>
          <span class="label">Search Latency</span>
        </div>
      </section>

      <section class="benchmarks">
        <h2>📊 Recent Benchmark Runs</h2>
        <table>
          <thead>
            <tr>
              <th>Test</th>
              <th>Score</th>
              <th>Status</th>
              <th>Timestamp</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let b of benchmarks" [class]="b.status">
              <td>{{ b.name }}</td>
              <td>{{ (b.score * 100).toFixed(1) }}%</td>
              <td><span class="badge" [class]="b.status">{{ b.status === 'pass' ? '✅' : b.status === 'fail' ? '❌' : '⚠️' }}</span></td>
              <td>{{ b.timestamp }}</td>
              <td><a [routerLink]="['/benchmark', b.id]" class="drill-link">View →</a></td>
            </tr>
          </tbody>
        </table>
      </section>

      <section class="explore">
        <a routerLink="/embeddings" class="feature-link">
          🌐 <strong>Phase Sphere</strong>
          <span>Explore OPTIC-K embeddings in 3D</span>
        </a>
      </section>

        <!-- Notebook Explorer -->
        <section class="card stats-grid">
          <div class="card-header">
            <h2>📓 Notebook Explorer</h2>
            <div class="header-controls">
                <input type="text" 
                       placeholder="Filter by name or tag..." 
                       class="search-input"
                       (input)="updateFilter($any($event.target).value)">
                <span class="badge">{{ filteredNotebooks.length }} Files</span>
            </div>
          </div>
          <div class="notebooks-list">
            <div *ngFor="let nb of filteredNotebooks" class="notebook-item">
              <a [routerLink]="['/notebook', nb.path]" class="nb-link">
                <span class="nb-icon">📄</span>
                <div class="nb-info">
                    <span class="nb-name">{{ nb.name }}</span>
                    <div class="nb-tags" *ngIf="nb.tags && nb.tags.length">
                        <span *ngFor="let tag of nb.tags" class="tag-pill">{{ tag }}</span>
                    </div>
                </div>
                <span class="nb-date">{{ nb.lastModified | date:'shortDate' }}</span>
              </a>
            </div>
            <div *ngIf="filteredNotebooks.length === 0" class="empty-state">No notebooks found matching "{{ searchTerm }}"</div>
          </div>
        </section>

      <section class="docs">
        <h2>📚 Documentation Explorer</h2>
        <div class="doc-grid">
          <a *ngFor="let doc of docLinks" [routerLink]="['/documentation', doc.path]" class="doc-card">
            <strong>{{ doc.title }}</strong>
            <p>{{ doc.description }}</p>
          </a>
          <div *ngIf="docLinks.length === 0" class="empty-state">No documentation files found in /Documentation</div>
        </div>
      </section>
    </div>
  `,
    styles: [`
    :host { display: block; min-height: 100vh; }
    .dashboard { padding: 16px 24px; font-family: system-ui, sans-serif; background: #f8f9fa; color: #333; min-height: 100vh; transition: all 0.3s; }
    .dashboard.dark { background: #1a1a2e; color: #e0e0e0; }
    
    .header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
    .header h1 { margin: 0; font-size: 1.25rem; }
    .header-right { display: flex; align-items: center; gap: 12px; }
    .dark .timestamp { color: #888; }
    .config-info { display: flex; gap: 8px; margin-right: 16px; }
    .badge.instrument { background: #e3f2fd; color: #1976d2; border: 1px solid #bbdefb; }
    .dark .badge.instrument { background: #0d47a1; color: #e3f2fd; border-color: #1565c0; }
    .badge.tuning { background: #f3e5f5; color: #7b1fa2; border: 1px solid #e1bee7; }
    .dark .badge.tuning { background: #4a148c; color: #f3e5f5; border-color: #6a1b9a; }
    .badge { padding: 4px 8px; border-radius: 4px; font-size: 0.75rem; font-weight: 600; }
    .theme-toggle { background: none; border: 1px solid #ddd; border-radius: 6px; padding: 4px 8px; cursor: pointer; font-size: 1rem; }
    .dark .theme-toggle { border-color: #444; }
    
    .metrics { display: grid; grid-template-columns: repeat(4, 1fr); gap: 12px; margin-bottom: 16px; }
    .metric-card { background: #fff; border-radius: 6px; padding: 12px; text-align: center; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }
    .dark .metric-card { background: #252540; }
    .metric-card .value { display: block; font-size: 1.5rem; font-weight: bold; color: #333; }
    .dark .metric-card .value { color: #e0e0e0; }
    .metric-card .label { font-size: 0.7rem; color: #666; text-transform: uppercase; letter-spacing: 0.05em; }
    .dark .metric-card .label { color: #888; }
    .metric-card.pass .value { color: #28a745; }
    .metric-card.warn .value { color: #ffc107; }
    .metric-card.fail .value { color: #dc3545; }
    
    .benchmarks { height: 300px; overflow-y: auto; border: 1px solid #eee; border-radius: 6px; }
    .benchmarks h2, .docs h2 { font-size: 0.9rem; margin-bottom: 8px; font-weight: 600; padding: 0 4px; }
    table { width: 100%; border-collapse: collapse; background: #fff; }
    .dark table { background: #252540; }
    thead { position: sticky; top: 0; z-index: 10; background: #f0f0f0; } 
    .dark thead { background: #1e1e32; }
    th, td { padding: 4px 8px; text-align: left; border-bottom: 1px solid #eee; font-size: 0.75rem; }
    .dark th, .dark td { border-color: #333; }
    th { font-weight: 600; text-transform: uppercase; letter-spacing: 0.05em; color: #555; }
    .dark th { color: #aaa; }
    
    .notebooks-list { display: flex; flex-direction: column; gap: 8px; margin-top: 12px; }
    .notebook-item { padding: 8px 12px; border: 1px solid #eee; border-radius: 6px; transition: all 0.2s; }
    .dark .notebook-item { border-color: #333; }
    .notebook-item:hover { transform: translateX(5px); background: #f0f4ff; }
    .dark .notebook-item:hover { background: #1e1e32; }
    .nb-link { display: flex; align-items: center; gap: 10px; text-decoration: none; color: inherit; font-size: 0.85rem; }
    .nb-icon { font-size: 1.1rem; }
    .nb-name { flex: 1; font-weight: 500; }
    .nb-date { font-size: 0.75rem; color: #888; }
    .empty-state { padding: 20px; text-align: center; color: #888; font-size: 0.85rem; }
    tr.pass { background: rgba(40, 167, 69, 0.1); }
    tr.fail { background: rgba(220, 53, 69, 0.1); }
    tr.warn { background: rgba(255, 193, 7, 0.1); }
    .badge { font-size: 0.75rem; }
    .drill-link { color: #0066cc; text-decoration: none; font-size: 0.75rem; }
    .dark .drill-link { color: #6db3f2; }
    .drill-link:hover { text-decoration: underline; }
    
    .explore { margin-bottom: 16px; }
    .feature-link { display: flex; align-items: center; gap: 8px; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: #fff; padding: 12px 16px; border-radius: 6px; text-decoration: none; transition: transform 0.2s, box-shadow 0.2s; }
    .feature-link:hover { transform: translateY(-2px); box-shadow: 0 4px 12px rgba(102, 126, 234, 0.4); }
    .feature-link strong { font-size: 1rem; }
    .feature-link span { font-size: 0.8rem; opacity: 0.9; }

    .card { background: #fff; border-radius: 6px; box-shadow: 0 1px 3px rgba(0,0,0,0.1); margin-bottom: 16px; padding: 12px; }
    .dark .card { background: #252540; }
    .card-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 10px; }
    .card-header h2 { margin: 0; font-size: 0.9rem; font-weight: 600; }
    .notebooks-list { display: grid; gap: 8px; }
    .notebook-item { background: #f8f9fa; border-radius: 4px; }
    .dark .notebook-item { background: #1e1e32; }
    .nb-link { display: flex; align-items: center; padding: 8px 10px; text-decoration: none; color: inherit; transition: background 0.2s; }
    .nb-link:hover { background: #e9ecef; }
    .dark .nb-link:hover { background: #333355; }
    .nb-icon { font-size: 1.2rem; margin-right: 8px; }
    .nb-info { flex-grow: 1; display: flex; flex-direction: column; gap: 4px; }
    .nb-name { font-size: 0.85rem; font-weight: 500; }
    .nb-tags { display: flex; gap: 4px; flex-wrap: wrap; }
    .tag-pill { background: #e9ecef; color: #555; padding: 2px 6px; border-radius: 10px; font-size: 0.65rem; border: 1px solid #dee2e6; }
    .dark .tag-pill { background: #333; color: #aaa; border-color: #444; }
    
    .nb-date { font-size: 0.7rem; color: #666; }
    .dark .nb-date { color: #888; }
    .empty-state { text-align: center; padding: 20px; font-size: 0.85rem; color: #888; }
    
    .header-controls { display: flex; align-items: center; gap: 12px; }
    .search-input { padding: 4px 8px; border-radius: 4px; border: 1px solid #ddd; font-size: 0.8rem; width: 200px; }
    .dark .search-input { background: #1a1a2e; border-color: #444; color: #fff; }
    
    .doc-grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 10px; }
    .doc-card { background: #e7f3ff; border-radius: 6px; padding: 10px; text-decoration: none; color: inherit; transition: transform 0.2s; }
    .dark .doc-card { background: #252540; }
    .doc-card:hover { transform: translateY(-2px); }
    .doc-card strong { display: block; color: #0066cc; margin-bottom: 2px; font-size: 0.8rem; }
    .dark .doc-card strong { color: #6db3f2; }
    .doc-card p { margin: 0; font-size: 0.7rem; color: #666; }
    .dark .doc-card p { color: #888; }
    
    @media (max-width: 900px) { .metrics, .doc-grid { grid-template-columns: repeat(2, 1fr); } }
    @media (max-width: 600px) { .metrics, .doc-grid { grid-template-columns: 1fr; } }
  `]
})
export class HomeComponent implements OnInit {
    private readonly navService = inject(BreadcrumbService);
    private readonly gaApi = inject(GaApiService);
    private readonly cdr = inject(ChangeDetectorRef);

    isDark = true; // Default to dark theme as requested
    lastUpdated = new Date().toLocaleString();
    voicingCount = 742815;
    embeddingDim = 216;
    avgScore = 0.87;
    searchLatencyMs = 42;
    instrument = 'Guitar';
    tuning = 'Standard (E A D G B E)';

    benchmarks: BenchmarkResult[] = [
        { id: 'beginner-open-c', name: 'Beginner Open C', score: 0.95, status: 'pass', timestamp: '2026-01-17 14:30' },
        { id: 'jazz-shell', name: 'Jazz Shell Voicing', score: 0.82, status: 'pass', timestamp: '2026-01-17 14:30' },
        { id: 'hendrix-chord', name: 'Hendrix Chord', score: 0.91, status: 'pass', timestamp: '2026-01-17 14:30' },
        { id: 'neo-soul', name: 'Neo-Soul Extensions', score: 0.78, status: 'warn', timestamp: '2026-01-17 14:30' },
        { id: 'power-chord', name: 'Power Chord Shapes', score: 0.98, status: 'pass', timestamp: '2026-01-17 14:30' },
    ];

    docLinks: DocLink[] = [];

    notebooks: Notebook[] = [];

    filteredNotebooks: Notebook[] = [];
    searchTerm = '';

    constructor() { }

    ngOnInit() {
        this.navService.setCrumbs([{ label: '🎸 Guitar Alchemist Dashboard' }]);

        this.gaApi.getNotebooks().subscribe({
            next: (data) => {
                console.log('[DASHBOARD] Notebooks received from API:', data);
                if (data && Array.isArray(data)) {
                    this.notebooks = data;
                    this.filterNotebooks();
                    console.log(`[DASHBOARD] Assigned ${this.notebooks.length} notebooks to local state.`);
                    this.cdr.detectChanges(); // Force view update
                } else {
                    console.error('[DASHBOARD] Unexpected notebooks data format (expected array):', data);
                    this.notebooks = [];
                    this.filteredNotebooks = [];
                }
            },
            error: (err) => {
                console.error('[DASHBOARD] Failed to load notebooks from GaAPI:', err);
                this.notebooks = [];
                this.filteredNotebooks = [];
                this.cdr.detectChanges();
            }
        });

        this.gaApi.getDocumentation().subscribe({
            next: (data) => {
                console.log('Docs loaded:', data);
                this.docLinks = data.map(d => ({
                    title: d.title,
                    path: d.path,
                    description: `Last modified: ${new Date(d.lastModified).toLocaleDateString()}`
                }));
            },
            error: (err) => console.error('Failed to load documentation', err)
        });
    }

    toggleTheme() {
        this.isDark = !this.isDark;
    }

    updateFilter(term: string) {
        this.searchTerm = term;
        this.filterNotebooks();
    }

    filterNotebooks() {
        if (!this.searchTerm) {
            this.filteredNotebooks = this.notebooks;
            return;
        }

        const term = this.searchTerm.toLowerCase();
        this.filteredNotebooks = this.notebooks.filter(nb =>
            nb.name.toLowerCase().includes(term) ||
            (nb.tags && nb.tags.some(t => t.toLowerCase().includes(term)))
        );
    }
}
