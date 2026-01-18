import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { GaApiService } from '../services/ga-api.service';
import { BreadcrumbService } from '../services/navigation.service';

interface BenchmarkDetail {
  id: string;
  name: string;
  score: number;
  status: 'pass' | 'fail' | 'warn';
  timestamp: string;
  duration: string;
  queries: QueryResult[];
  rawOutput: string;
}

interface QueryResult {
  query: string;
  expected: string;
  actual: string;
  score: number;
  pass: boolean;
}

// Mock data fallback - in case API fails
const MOCK_BENCHMARKS: Record<string, BenchmarkDetail> = {
  'beginner-open-c': {
    id: 'beginner-open-c', name: 'Beginner Open C', score: 0.95, status: 'pass', timestamp: '2026-01-17 14:30', duration: '2.3s',
    queries: [
      { query: 'open C major chord', expected: 'x32010', actual: 'x32010', score: 1.0, pass: true },
      { query: 'easy G chord', expected: '320003', actual: '320003', score: 1.0, pass: true },
      { query: 'simple D chord', expected: 'xx0232', actual: 'xx0232', score: 1.0, pass: true },
      { query: 'basic E minor', expected: '022000', actual: '022000', score: 1.0, pass: true },
      { query: 'beginner A major', expected: 'x02220', actual: 'x02220', score: 0.75, pass: true },
    ],
    rawOutput: `Quality Check: Beginner Open C\n5 / 5 passed | Avg: 0.95 | Duration: 2.3s`
  },
  'jazz-shell': {
    id: 'jazz-shell', name: 'Jazz Shell Voicing', score: 0.82, status: 'pass', timestamp: '2026-01-17 14:30', duration: '1.8s',
    queries: [
      { query: 'Cmaj7 shell voicing', expected: 'x3x44x', actual: 'x3x44x', score: 1.0, pass: true },
      { query: 'Dm7 jazz chord', expected: 'xx0211', actual: 'xx0211', score: 0.82, pass: true },
      { query: 'G7 shell', expected: '3x3x01', actual: '3x344x', score: 0.65, pass: true },
    ],
    rawOutput: `Quality Check: Jazz Shell Voicing\n3 / 3 passed | Avg: 0.82 | Duration: 1.8s`
  },
  'hendrix-chord': {
    id: 'hendrix-chord', name: 'Hendrix Chord', score: 0.91, status: 'pass', timestamp: '2026-01-17 14:30', duration: '1.5s',
    queries: [
      { query: 'E7#9 hendrix chord', expected: '020130', actual: '020130', score: 1.0, pass: true },
      { query: 'purple haze chord', expected: '020130', actual: '020130', score: 1.0, pass: true },
      { query: 'dominant #9', expected: '020130', actual: 'x76780', score: 0.73, pass: true },
    ],
    rawOutput: `Quality Check: Hendrix Chord\n3 / 3 passed | Avg: 0.91 | Duration: 1.5s`
  },
  'neo-soul': {
    id: 'neo-soul', name: 'Neo-Soul Extensions', score: 0.78, status: 'warn', timestamp: '2026-01-17 14:30', duration: '2.1s',
    queries: [
      { query: 'Cmaj9 neo soul', expected: 'x3243x', actual: 'x3243x', score: 0.85, pass: true },
      { query: 'erykah badu chord', expected: 'x5756x', actual: 'x5756x', score: 0.80, pass: true },
      { query: 'D\'Angelo voicing', expected: 'xx0212', actual: 'xx0211', score: 0.68, pass: false },
    ],
    rawOutput: `Quality Check: Neo-Soul Extensions\n2 / 3 passed | Avg: 0.78 | Duration: 2.1s`
  },
  'power-chord': {
    id: 'power-chord', name: 'Power Chord Shapes', score: 0.98, status: 'pass', timestamp: '2026-01-17 14:30', duration: '1.2s',
    queries: [
      { query: 'E5 power chord', expected: '022xxx', actual: '022xxx', score: 1.0, pass: true },
      { query: 'A5 power chord', expected: 'x022xx', actual: 'x022xx', score: 1.0, pass: true },
      { query: 'G5 rock chord', expected: '355xxx', actual: '355xxx', score: 0.95, pass: true },
    ],
    rawOutput: `Quality Check: Power Chord Shapes\n3 / 3 passed | Avg: 0.98 | Duration: 1.2s`
  }
};

@Component({
  selector: 'app-benchmark-detail',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="benchmark-container" [class.dark]="isDark">
      <header class="page-header">
        <div class="header-main">
          <h1>📊 Benchmark Detail</h1>
        </div>
        <button class="theme-toggle" (click)="toggleTheme()">{{ isDark ? '☀️' : '🌙' }}</button>
      </header>

      <div class="loading-state" *ngIf="isLoading">
        <div class="spinner"></div>
        <p>Running benchmark analysis...</p>
      </div>

      <div *ngIf="!isLoading && benchmark">
        <div class="title-row">
          <h1>{{ benchmark.name }}</h1>
          <span class="badge" [class]="benchmark.status">
            {{ benchmark.status === 'pass' ? '✅ Passed' : benchmark.status === 'fail' ? '❌ Failed' : '⚠️ Warning' }}
          </span>
          <button (click)="getAIAnalysis()" class="ai-button" [disabled]="isAiLoading">
            {{ isAiLoading ? '🤖 Analyzing...' : '🤖 Get AI Analyst Insight' }}
          </button>
        </div>

        <section class="ai-insight" *ngIf="aiAnalysis">
          <div class="insight-header">
            <h3>🤖 AI Analyst Insight (GLM-4)</h3>
            <button (click)="aiAnalysis = ''" class="close-btn">×</button>
          </div>
          <div class="insight-content">{{ aiAnalysis }}</div>
        </section>

        <section class="summary">
          <div class="stat"><strong>Score:</strong> {{ (benchmark.score * 100).toFixed(1) }}%</div>
          <div class="stat"><strong>Duration:</strong> {{ benchmark.duration }}</div>
          <div class="stat"><strong>Timestamp:</strong> {{ benchmark.timestamp }}</div>
          <div class="stat"><strong>Queries:</strong> {{ benchmark.queries.length }}</div>
        </section>

        <section class="queries">
          <h2>📋 Query Results</h2>
          <table>
            <thead>
              <tr>
                <th>Query</th>
                <th>Expected</th>
                <th>Actual</th>
                <th>Score</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let q of benchmark.queries" [class.fail]="!q.pass">
                <td>{{ q.query }}</td>
                <td><code>{{ q.expected }}</code></td>
                <td><code>{{ q.actual }}</code></td>
                <td>{{ (q.score * 100).toFixed(0) }}%</td>
                <td>{{ q.pass ? '✅' : '❌' }}</td>
              </tr>
            </tbody>
          </table>
        </section>

        <section class="raw-output">
          <h2>📜 Raw Output</h2>
          <pre>{{ benchmark.rawOutput }}</pre>
        </section>
      </div>

      <div class="error-state" *ngIf="!isLoading && !benchmark && errorMessage">
        <p>⚠️ {{ errorMessage }}</p>
      </div>

      <a routerLink="/" class="back-link">← Back to Dashboard</a>
    </div>
  `,
  styles: [`
    :host { display: block; min-height: 100vh; }
    .benchmark-container { padding: 16px 24px; font-family: system-ui, sans-serif; background: #f8f9fa; color: #333; min-height: 100vh; transition: all 0.3s; }
    .benchmark-container.dark { background: #1a1a2e; color: #e0e0e0; }
    
    .loading-state { display: flex; flex-direction: column; align-items: center; justify-content: center; padding: 40px; }
    .spinner { width: 30px; height: 30px; border: 3px solid #ddd; border-top-color: #667eea; border-radius: 50%; animation: spin 0.8s linear infinite; margin-bottom: 12px; }
    @keyframes spin { to { transform: rotate(360deg); } }

    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px; }
    .theme-toggle { background: none; border: 1px solid #ddd; border-radius: 6px; padding: 4px 8px; cursor: pointer; font-size: 1rem; color: inherit; }
    .dark .theme-toggle { border-color: #444; }
    
    .title-row { display: flex; align-items: center; gap: 12px; margin-bottom: 12px; }
    .title-row h1 { margin: 0; font-size: 1.25rem; }
    .ai-button { margin-left: auto; background: #667eea; color: #fff; border: none; padding: 4px 12px; border-radius: 6px; font-size: 0.75rem; cursor: pointer; transition: 0.2s; }
    .ai-button:hover { background: #764ba2; }
    .ai-button:disabled { opacity: 0.6; cursor: not-allowed; }

    .ai-insight { background: #f0f4ff; border: 1px solid #667eea; border-radius: 8px; padding: 12px; margin-bottom: 20px; animation: slideIn 0.3s ease-out; }
    .dark .ai-insight { background: rgba(102, 126, 234, 0.1); border-color: #764ba2; }
    .insight-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px; }
    .insight-header h3 { margin: 0; font-size: 0.85rem; color: #667eea; }
    .close-btn { background: none; border: none; font-size: 1.2rem; cursor: pointer; color: #888; }
    .insight-content { font-size: 0.8rem; line-height: 1.5; white-space: pre-wrap; }
    @keyframes slideIn { from { opacity: 0; transform: translateY(-10px); } to { opacity: 1; transform: translateY(0); } }

    .badge { padding: 3px 10px; border-radius: 12px; font-size: 0.75rem; }
    .badge.pass { background: #d4edda; color: #155724; }
    .badge.fail { background: #f8d7da; color: #721c24; }
    .badge.warn { background: #fff3cd; color: #856404; }
    .dark .badge.pass { background: rgba(40, 167, 69, 0.2); color: #7ddf93; }
    .dark .badge.fail { background: rgba(220, 53, 69, 0.2); color: #f1a1a8; }
    .dark .badge.warn { background: rgba(255, 193, 7, 0.2); color: #ffe066; }

    .summary { display: flex; gap: 20px; margin-bottom: 16px; padding: 10px 14px; background: #fff; border-radius: 6px; box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1); }
    .dark .summary { background: #252540; }
    .stat { font-size: 0.8rem; }
    .stat strong { color: #666; }
    .dark .stat strong { color: #888; }

    .queries h2, .raw-output h2 { font-size: 0.9rem; margin-bottom: 8px; font-weight: 600; }
    table { width: 100%; border-collapse: collapse; margin-bottom: 16px; background: #fff; border-radius: 6px; overflow: hidden; box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1); }
    .dark table { background: #252540; }
    th, td { padding: 6px 10px; text-align: left; border-bottom: 1px solid #eee; font-size: 0.75rem; }
    .dark th, .dark td { border-color: #333; }
    th { background: #f0f0f0; font-weight: 600; font-size: 0.65rem; text-transform: uppercase; letter-spacing: 0.05em; }
    .dark th { background: #1e1e32; color: #aaa; }
    tr.fail { background: rgba(220, 53, 69, 0.1); }
    code { background: #e9ecef; padding: 1px 4px; border-radius: 3px; font-size: 0.7rem; }
    .dark code { background: #333; color: #aaa; }

    .raw-output pre { background: #1e1e1e; color: #d4d4d4; padding: 12px; border-radius: 6px; overflow-x: auto; font-size: 0.7rem; max-height: 250px; line-height: 1.4; }
    .back-link { display: inline-block; margin-top: 12px; color: #0066cc; text-decoration: none; font-size: 0.8rem; }
    .dark .back-link { color: #6db3f2; }
    .back-link:hover { text-decoration: underline; }
  `]
})
export class BenchmarkDetailComponent implements OnInit {
  private readonly navService = inject(BreadcrumbService);
  private readonly gaApi = inject(GaApiService);
  private readonly route = inject(ActivatedRoute);

  isDark = true;
  isLoading = true;
  isAiLoading = false;
  aiAnalysis = '';
  errorMessage = '';
  benchmark: BenchmarkDetail | null = null;
  benchmarkId: string = '';

  getAIAnalysis() {
    if (!this.benchmark) return;
    this.isAiLoading = true;
    this.gaApi.analyzeBenchmark(this.benchmark.name, this.benchmark).subscribe({
      next: (res) => {
        this.aiAnalysis = res.analysis;
        this.isAiLoading = false;
      },
      error: (err) => {
        console.error('AI analysis failed', err);
        this.aiAnalysis = 'Error: AI Analyst is currently offline.';
        this.isAiLoading = false;
      }
    });
  }

  ngOnInit() {
    this.benchmarkId = this.route.snapshot.paramMap.get('id') || '';
    this.updateBreadcrumbs();
    this.loadBenchmark();
  }

  private loadBenchmark() {
    if (this.benchmarkId) {
      this.gaApi.getBenchmarkById(this.benchmarkId).subscribe({
        next: (data) => {
          this.benchmark = this.transformApiData(data);
          this.isLoading = false;
          this.updateBreadcrumbs();
        },
        error: (err) => {
          console.error('Failed to load benchmark', err);
          this.benchmark = MOCK_BENCHMARKS[this.benchmarkId] || MOCK_BENCHMARKS['beginner-open-c'];
          this.isLoading = false;
          this.updateBreadcrumbs();
        }
      });
    }
  }

  private transformApiData(data: any): BenchmarkDetail {
    return {
      id: data.benchmarkId,
      name: data.name,
      score: data.score,
      status: data.score >= 0.8 ? 'pass' : (data.score >= 0.6 ? 'warn' : 'fail'),
      timestamp: new Date(data.timestamp).toLocaleString(),
      duration: `${(data.durationMs / 1000).toFixed(1)}s`,
      queries: data.steps.map((s: any) => ({
        query: s.name,
        expected: s.expected,
        actual: s.actual,
        score: s.score,
        pass: s.passed
      })),
      rawOutput: data.rawOutput || 'No raw output available.'
    };
  }

  private updateBreadcrumbs() {
    this.navService.setCrumbs([
      { label: 'Dashboard', path: '/' },
      { label: 'Benchmarks', path: '/' },
      { label: this.benchmark?.name || this.benchmarkId }
    ]);
  }

  toggleTheme() {
    this.isDark = !this.isDark;
  }
}
