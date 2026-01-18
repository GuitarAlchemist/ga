import { Component, ElementRef, OnInit, OnDestroy, ViewChild, AfterViewInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { GaApiService, VoicingWithEmbedding } from '../services/ga-api.service';
import { BreadcrumbService } from '../services/navigation.service';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { Subscription, interval, Subject, timer, of } from 'rxjs';
import { takeWhile, takeUntil, catchError, switchMap } from 'rxjs/operators';

interface VoicingPoint {
    id: string;
    name: string;
    notes: string;
    fretboard: string;
    type: string;
    position: [number, number, number];
    cagedType: string;
    harmonicAxes: {
        tonality: number;
        complexity: number;
        brightness: number;
    };
    color: number;
}

const MOCK_VOICINGS: VoicingPoint[] = [
    { id: '1', name: 'C Major', notes: 'C E G', fretboard: 'x32010', type: 'Open', position: [0.5, 0.2, -0.1], cagedType: 'C-Shape', harmonicAxes: { tonality: 0.9, complexity: 0.2, brightness: 0.6 }, color: 0x667eea },
    { id: '2', name: 'G Major', notes: 'G B D', fretboard: '320003', type: 'Open', position: [-0.3, 0.8, 0.1], cagedType: 'G-Shape', harmonicAxes: { tonality: 0.85, complexity: 0.1, brightness: 0.7 }, color: 0x764ba2 },
    { id: '3', name: 'D7', notes: 'D F# A C', fretboard: 'xx0212', type: 'Dominant', position: [0.1, -0.4, 0.9], cagedType: 'D-Shape', harmonicAxes: { tonality: 0.7, complexity: 0.5, brightness: 0.8 }, color: 0xff6b6b },
    { id: '4', name: 'Am7', notes: 'A C E G', fretboard: 'x02010', type: 'Minor 7', position: [-0.7, -0.1, -0.5], cagedType: 'A-Shape', harmonicAxes: { tonality: 0.6, complexity: 0.4, brightness: 0.3 }, color: 0x4ecdc4 },
    { id: '5', name: 'E9', notes: 'E G# B D F#', fretboard: '020102', type: 'Extension', position: [0.2, 0.6, -0.8], cagedType: 'E-Shape', harmonicAxes: { tonality: 0.75, complexity: 0.8, brightness: 0.9 }, color: 0xffd93d },
    { id: '6', name: 'Fmaj7', notes: 'F A C E', fretboard: '1x221x', type: 'Shell', position: [0.8, -0.7, 0.2], cagedType: 'E-Shape', harmonicAxes: { tonality: 0.8, complexity: 0.6, brightness: 0.5 }, color: 0x6ab04c },
];

@Component({
    selector: 'app-embedding-viewer',
    standalone: true,
    imports: [CommonModule, RouterLink, FormsModule],
    template: `
    <div class="viewer-page" [class.dark]="isDark">
      <header class="page-header">
        <div class="header-main">
          <h1>🌌 Phase Sphere Explorer</h1>
        </div>
        <div class="header-controls">
          <div class="search-box" *ngIf="useApiData">
            <input type="text" [(ngModel)]="ngModelSearchQuery" placeholder="Search chords..." (keyup.enter)="loadFromApi()" />
            <button (click)="loadFromApi()">Search</button>
          </div>
          <button class="toggle-btn" (click)="toggleDataSource()">
            {{ useApiData ? 'Use Mock Data' : 'Connect to GaAPI' }}
          </button>
          <button class="theme-toggle" (click)="toggleTheme()">{{ isDark ? '☀️' : '🌙' }}</button>
        </div>
      </header>

      <main class="viewer-layout">
        <div class="canvas-container" #canvasContainer>
          <div class="loading-overlay" *ngIf="isLoading">
            <div class="spinner"></div>
            <p>Loading Voicing Embeddings...</p>
          </div>
          <div class="progress-bar" *ngIf="isAiLoading">
             <div class="progress-fill" [style.width.%]="aiProgress"></div>
             <span class="eta">Analyzing... ETA {{ remainingSeconds }}s</span>
             <button class="cancel-btn" (click)="cancelAiAnalysis()">Cancel</button>
          </div>
          <div class="error-msg" *ngIf="errorMessage">
            ⚠️ {{ errorMessage }}
            <button (click)="loadFromApi()">Retry</button>
          </div>
        </div>

        <aside class="side-panel" *ngIf="selectedVoicing">
          <h2>Voicing Details</h2>
          <p><strong>Chord:</strong> {{ selectedVoicing.name }}</p>
          <p *ngIf="selectedVoicing.notes"><strong>Notes:</strong> {{ selectedVoicing.notes }}</p>
          <p><strong>Type:</strong> {{ selectedVoicing.type }}</p>
          <p><strong>Axis:</strong> {{ selectedVoicing.cagedType }}</p>
          <p><strong>Position:</strong> {{ selectedVoicing.fretboard }}</p>

          <div class="ai-analysis" *ngIf="isAiLoading || aiAnalysis">
            <h4>🤖 AI Harmonic Analysis</h4>
            <div class="ai-loader" *ngIf="isAiLoading">
              <span class="dot"></span><span class="dot"></span><span class="dot"></span>
            </div>
            <p class="ai-content" *ngIf="aiAnalysis">{{ aiAnalysis }}</p>
          </div>

          <div class="harmonic-stats">
            <h3>Harmonic Axes</h3>
            <div class="stat-row">
              <span class="label">Tonality</span>
              <div class="bar-container"><div class="bar" [style.width.%]="selectedVoicing.harmonicAxes.tonality * 100"></div></div>
            </div>
            <div class="stat-row">
              <span class="label">Complexity</span>
              <div class="bar-container"><div class="bar" [style.width.%]="selectedVoicing.harmonicAxes.complexity * 100"></div></div>
            </div>
            <div class="stat-row">
              <span class="label">Brightness</span>
              <div class="bar-container"><div class="bar" [style.width.%]="selectedVoicing.harmonicAxes.brightness * 100"></div></div>
            </div>
          </div>

          <button (click)="getAiExplanation()" class="ai-btn" [disabled]="isAiLoading">
            {{ isAiLoading ? '🤖 Analyzing...' : '🤖 Explain with AI' }}
          </button>
        </aside>

        <aside class="side-panel" *ngIf="!selectedVoicing">
          <h3>Phase Sphere Index</h3>
          <p>Select a point in the 3D embedding space to view harmonic details.</p>
          <p>Discover relationships between voicings through their proximity in Phase Space.</p>
          <div class="legend">
            <p><span class="dot" style="background: #667eea"></span> Open Chords</p>
            <p><span class="dot" style="background: #ff6b6b"></span> Dominant</p>
            <p><span class="dot" style="background: #4ecdc4"></span> Minor</p>
            <p><span class="dot" style="background: #ffd93d"></span> Extensions</p>
          </div>
          <a routerLink="/" class="back-link">← Back to Dashboard</a>
        </aside>
      </main>
    </div>
  `,
    styles: [`
    :host { display: block; height: 100vh; overflow: hidden; }
    .viewer-page { height: 100vh; display: flex; flex-direction: column; background: #f0f2f5; color: #333; font-family: system-ui, sans-serif; transition: all 0.3s; }
    .viewer-page.dark { background: #1a1a2e; color: #e0e0e0; }

    .page-header { display: flex; justify-content: space-between; align-items: center; padding: 12px 24px; background: #fff; box-shadow: 0 2px 4px rgba(0,0,0,0.05); z-index: 10; }
    .dark .page-header { background: #161625; border-bottom: 1px solid #333; }
    .header-main h1 { margin: 0; font-size: 1.2rem; }
    .header-controls { display: flex; gap: 12px; align-items: center; }
    
    .search-box { display: flex; gap: 4px; }
    .search-box input { padding: 4px 8px; border: 1px solid #ddd; border-radius: 4px; font-size: 0.85rem; }
    .toggle-btn { padding: 4px 12px; background: #eee; border: 1px solid #ccc; border-radius: 4px; font-size: 0.85rem; cursor: pointer; }
    .theme-toggle { background: none; border: 1px solid #ddd; border-radius: 6px; padding: 4px 8px; cursor: pointer; }

    .viewer-layout { flex: 1; display: flex; position: relative; overflow: hidden; }
    .canvas-container { flex: 1; min-width: 0; position: relative; }
    
    .loading-overlay { position: absolute; inset: 0; display: flex; flex-direction: column; align-items: center; justify-content: center; background: rgba(255,255,255,0.8); z-index: 5; }
    .dark .loading-overlay { background: rgba(26,26,46,0.8); }
    .spinner { width: 40px; height: 40px; border: 4px solid #f3f3f3; border-top: 4px solid #667eea; border-radius: 50%; animation: spin 1s linear infinite; }
    @keyframes spin { 0% { transform: rotate(0deg); } 100% { transform: rotate(360deg); } }

    .side-panel { width: 300px; background: #fff; padding: 20px; border-left: 1px solid #eee; overflow-y: auto; box-shadow: -2px 0 8px rgba(0,0,0,0.05); }
    .dark .side-panel { background: #161625; border-color: #333; }
    .side-panel h2 { margin-top: 0; font-size: 1.1rem; border-bottom: 2px solid #667eea; padding-bottom: 8px; }
    .side-panel h3 { font-size: 1rem; color: #667eea; }
    .side-panel p { font-size: 0.85rem; margin: 8px 0; }
    
    .harmonic-stats { margin-top: 24px; padding-top: 16px; border-top: 1px solid #eee; }
    .dark .harmonic-stats { border-color: #333; }
    .stat-row { margin-bottom: 12px; }
    .stat-row .label { display: block; font-size: 0.75rem; color: #888; margin-bottom: 4px; text-transform: uppercase; }
    .bar-container { height: 6px; background: #eee; border-radius: 3px; overflow: hidden; }
    .dark .bar-container { background: #333; }
    .bar { height: 100%; background: linear-gradient(90deg, #667eea 0%, #764ba2 100%); transition: width 0.5s ease-out; }
    
    .ai-btn { width: 100%; margin-top: 20px; padding: 10px; background: #667eea; color: #fff; border: none; border-radius: 6px; cursor: pointer; font-weight: 600; transition: background 0.2s; }
    .ai-btn:hover { background: #5a6fd6; }
    .ai-btn:disabled { opacity: 0.7; cursor: not-allowed; }

    .ai-analysis { background: rgba(102, 126, 234, 0.1); border-radius: 8px; padding: 12px; margin-top: 16px; border: 1px solid rgba(102, 126, 234, 0.2); }
    .ai-analysis h4 { margin: 0 0 8px 0; font-size: 0.85rem; color: #667eea; }
    .ai-content { font-size: 0.8rem !important; line-height: 1.5; font-style: italic; white-space: pre-wrap; }

    .progress-bar { position: absolute; top: 10px; left: 50%; transform: translateX(-50%); width: 300px; background: rgba(0,0,0,0.7); border-radius: 20px; padding: 10px 15px; z-index: 100; color: #fff; }
    .progress-fill { height: 4px; background: #667eea; border-radius: 2px; transition: width 0.3s; margin-bottom: 5px; }
    .eta { font-size: 11px; }
    .cancel-btn { float: right; background: none; border: none; color: #ff6b6b; cursor: pointer; font-size: 11px; }
    
    .legend { margin-top: 20px; font-size: 0.8rem; }
    .legend .dot { display: inline-block; width: 10px; height: 10px; border-radius: 50%; margin-right: 6px; }
    .back-link { display: inline-block; margin-top: 24px; color: #667eea; text-decoration: none; font-size: 0.85rem; }
    .back-link:hover { text-decoration: underline; }
  `]
})
export class EmbeddingViewerComponent implements OnInit, AfterViewInit, OnDestroy {
    @ViewChild('canvasContainer', { static: true }) canvasContainer!: ElementRef<HTMLDivElement>;

    private readonly navService = inject(BreadcrumbService);
    private readonly gaApi = inject(GaApiService);

    isDark = true;
    voicings: VoicingPoint[] = MOCK_VOICINGS;
    selectedVoicing: VoicingPoint | null = null;

    useApiData = false;
    isLoading = false;
    errorMessage = '';
    ngModelSearchQuery = 'guitar chords';

    isAiLoading = false;
    aiAnalysis = '';
    aiProgress = 0;
    remainingSeconds = 0;
    private destroy$ = new Subject<void>();

    private scene!: THREE.Scene;
    private camera!: THREE.PerspectiveCamera;
    private renderer!: THREE.WebGLRenderer;
    private controls!: OrbitControls;
    private points: THREE.Points | null = null;
    private resizeSubscription!: Subscription;

    ngOnInit() {
        this.navService.setCrumbs([
            { label: 'Dashboard', path: '/' },
            { label: 'Phase Sphere Explorer' }
        ]);
        if (this.useApiData) this.loadFromApi();
    }

    ngAfterViewInit() {
        this.initThree();
        this.animate();

        window.addEventListener('resize', this.onWindowResize.bind(this));
        this.canvasContainer.nativeElement.addEventListener('click', this.onCanvasClick.bind(this));
    }

    ngOnDestroy() {
        this.destroy$.next();
        this.destroy$.complete();
        window.removeEventListener('resize', this.onWindowResize);
        if (this.renderer) {
            this.renderer.dispose();
            this.canvasContainer.nativeElement.removeChild(this.renderer.domElement);
        }
    }

    toggleDataSource() {
        this.useApiData = !this.useApiData;
        if (this.useApiData) {
            this.loadFromApi();
        } else {
            this.voicings = MOCK_VOICINGS;
            this.updatePoints();
        }
    }

    loadFromApi() {
        this.isLoading = true;
        this.errorMessage = '';
        this.gaApi.searchVoicings(this.ngModelSearchQuery).subscribe({
            next: (data) => {
                this.voicings = data.map(v => this.transformVoicing(v));
                this.updatePoints();
                this.isLoading = false;
            },
            error: (err) => {
                console.error('API Error:', err);
                this.errorMessage = 'Failed to connect to GaAPI service.';
                this.isLoading = false;
            }
        });
    }

    getAiExplanation() {
        if (!this.selectedVoicing) return;

        this.isAiLoading = true;
        this.aiAnalysis = '';
        this.aiProgress = 0;
        this.remainingSeconds = 15;

        // Progress simulation
        timer(0, 150).pipe(
            takeWhile(v => v <= 100 && this.isAiLoading),
            takeUntil(this.destroy$)
        ).subscribe(v => {
            this.aiProgress = v;
            if (v % 7 === 0) this.remainingSeconds = Math.max(0, 15 - Math.floor(v / 7));
        });

        this.gaApi.explainVoicing(this.selectedVoicing.name, this.selectedVoicing.notes || '').pipe(
            catchError(err => {
                console.error('AI analysis error', err);
                return of({ explanation: 'AI Analyst is currently unavailable. Please ensure Ollama is running with glm4 model.' } as any);
            }),
            takeUntil(this.destroy$)
        ).subscribe((res: any) => {
            this.aiAnalysis = res.explanation;
            this.isAiLoading = false;
        });
    }

    cancelAiAnalysis() {
        this.isAiLoading = false;
        this.aiAnalysis = 'Analysis cancelled by user.';
    }

    private transformVoicing(v: VoicingWithEmbedding): VoicingPoint {
        return {
            id: v.id,
            name: v.name || 'Unknown Chord',
            notes: (v as any).notes || (v.frets ? v.frets.join(' ') : ''),
            fretboard: (v as any).fretboard || (v.frets ? v.frets.join('') : 'Unknown'),
            type: v.chordType || 'Standard',
            position: [v.position.x, v.position.y, v.position.z],
            cagedType: v.cagedShape || 'Derived',
            harmonicAxes: {
                tonality: Math.random(),
                complexity: Math.random(),
                brightness: Math.random()
            },
            color: this.getColorByType(v.chordType)
        };
    }

    private getColorByType(type: string): number {
        if (type?.includes('Open')) return 0x667eea;
        if (type?.includes('Dominant')) return 0xff6b6b;
        if (type?.includes('Minor')) return 0x4ecdc4;
        if (type?.includes('Extension')) return 0xffd93d;
        return 0x95a5a6;
    }

    private initThree() {
        this.scene = new THREE.Scene();
        this.camera = new THREE.PerspectiveCamera(75, this.canvasContainer.nativeElement.clientWidth / this.canvasContainer.nativeElement.clientHeight, 0.1, 1000);
        this.camera.position.z = 5;

        this.renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
        this.renderer.setSize(this.canvasContainer.nativeElement.clientWidth, this.canvasContainer.nativeElement.clientHeight);
        this.renderer.setPixelRatio(window.devicePixelRatio);
        this.canvasContainer.nativeElement.appendChild(this.renderer.domElement);

        this.controls = new OrbitControls(this.camera, this.renderer.domElement);
        this.controls.enableDamping = true;

        this.updatePoints();
    }

    private updatePoints() {
        if (this.points) this.scene.remove(this.points);

        const geometry = new THREE.BufferGeometry();
        const positions = new Float32Array(this.voicings.length * 3);
        const colors = new Float32Array(this.voicings.length * 3);

        this.voicings.forEach((v, i) => {
            positions[i * 3] = v.position[0] * 3;
            positions[i * 3 + 1] = v.position[1] * 3;
            positions[i * 3 + 2] = v.position[2] * 3;

            const color = new THREE.Color(v.color);
            colors[i * 3] = color.r;
            colors[i * 3 + 1] = color.g;
            colors[i * 3 + 2] = color.b;
        });

        geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));
        geometry.setAttribute('color', new THREE.BufferAttribute(colors, 3));

        const material = new THREE.PointsMaterial({
            size: 0.15,
            vertexColors: true,
            transparent: true,
            opacity: 0.8
        });

        this.points = new THREE.Points(geometry, material);
        this.scene.add(this.points);
    }

    private animate() {
        requestAnimationFrame(() => this.animate());
        this.controls.update();
        this.renderer.render(this.scene, this.camera);
    }

    private onWindowResize() {
        this.camera.aspect = this.canvasContainer.nativeElement.clientWidth / this.canvasContainer.nativeElement.clientHeight;
        this.camera.updateProjectionMatrix();
        this.renderer.setSize(this.canvasContainer.nativeElement.clientWidth, this.canvasContainer.nativeElement.clientHeight);
    }

    private onCanvasClick(event: MouseEvent) {
        const rect = this.renderer.domElement.getBoundingClientRect();
        const mouse = new THREE.Vector2(
            ((event.clientX - rect.left) / rect.width) * 2 - 1,
            -((event.clientY - rect.top) / rect.height) * 2 + 1
        );

        const raycaster = new THREE.Raycaster();
        raycaster.setFromCamera(mouse, this.camera);

        if (this.points) {
            const intersects = raycaster.intersectObject(this.points);
            if (intersects.length > 0) {
                const index = intersects[0].index!;
                this.selectedVoicing = this.voicings[index];
                this.aiAnalysis = ''; // Reset analysis on new selection
            } else {
                this.selectedVoicing = null;
            }
        }
    }

    toggleTheme() {
        this.isDark = !this.isDark;
    }
}
