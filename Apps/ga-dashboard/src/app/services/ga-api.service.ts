import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map, catchError, of } from 'rxjs';

// Types matching the GaApi models
export interface ChordSearchResult {
    id: string;
    chordName: string;
    similarityScore: number;
    chordData: Record<string, unknown>;
    searchedAt: string;
}

export interface IndexStats {
    totalVoicings: number;
    embeddingDimensions: number;
    averageLatencyMs: number;
    lastUpdated: string;
}

export interface VoicingWithEmbedding {
    id: string;
    name: string;
    chordType: string;
    quality: string;
    extension: string;
    position: { x: number; y: number; z: number };
    harmonicAxis: string;
    embedding: number[];
    // Additional properties from ChordData
    frets?: number[];
    difficulty?: number;
    cagedShape?: string;
    intervalPattern?: string[];
}

// Harmonic axis assignments based on chord properties
const HARMONIC_AXIS_COLORS: Record<string, string> = {
    'Major': '#3498db',        // Fifths (k=5) - Tonal
    'Minor': '#9b59b6',        // Minor-third (k=3)
    'Dominant': '#e74c3c',     // Tritone (k=6)
    'Diminished': '#2ecc71',   // Minor-third (k=3)
    'Augmented': '#f39c12',    // Major-third (k=4)
    'Minor7': '#1abc9c',       // Mixed
    'Major7': '#00bcd4',       // Mixed
    'Half-Diminished': '#ff9800', // Diminished variant
    'default': '#95a5a6'
};

const HARMONIC_AXIS_LABELS: Record<string, string> = {
    'Major': 'Fifths (k=5) - Tonal',
    'Minor': 'Minor-third (k=3)',
    'Dominant': 'Tritone (k=6)',
    'Diminished': 'Minor-third (k=3)',
    'Augmented': 'Major-third (k=4)',
    'Minor7': 'Fifths (k=5)',
    'Major7': 'Fifths (k=5)',
    'Half-Diminished': 'Minor-third (k=3)',
    'default': 'Mixed'
};

@Injectable({
    providedIn: 'root'
})
export class GaApiService {
    private readonly apiUrl = '/api';

    constructor(private http: HttpClient) { }

    /**
     * Semantic search for voicings using natural language
     */
    semanticSearch(query: string, limit: number = 50): Observable<ChordSearchResult[]> {
        const params = new HttpParams()
            .set('q', query)
            .set('limit', limit.toString());

        return this.http.get<ChordSearchResult[]>(`${this.apiUrl}/vectorsearch/semantic`, { params })
            .pipe(
                catchError(err => {
                    console.error('Semantic search failed:', err);
                    return of([]);
                })
            );
    }

    /**
     * Search voicings and return visualization-friendly objects
     */
    searchVoicings(query: string, limit: number = 50): Observable<VoicingWithEmbedding[]> {
        return this.semanticSearch(query, limit).pipe(
            map(results => this.transformToVoicingPoints(results))
        );
    }

    getBenchmarks(): Observable<any[]> {
        return this.http.get<any[]>(`${this.apiUrl}/benchmark`);
    }

    getBenchmarkById(id: string): Observable<any> {
        return this.http.get<any>(`${this.apiUrl}/benchmark/${id}`);
    }

    runBenchmark(id: string): Observable<any> {
        return this.http.post<any>(`${this.apiUrl}/benchmark/run/${id}`, {});
    }

    getNotebooks(): Observable<any[]> {
        return this.http.get<any[]>(`${this.apiUrl}/notebook`);
    }

    getNotebook(path: string): Observable<any> {
        return this.http.get<any>(`${this.apiUrl}/notebook/${path}`);
    }

    getDocumentation(): Observable<any[]> {
        return this.http.get<any[]>(`${this.apiUrl}/documentation`);
    }

    getDocContent(path: string): Observable<string> {
        return this.http.get(`${this.apiUrl}/documentation/${path}`, { responseType: 'text' });
    }

    analyzeBenchmark(name: string, data: any): Observable<{ analysis: string }> {
        return this.http.post<{ analysis: string }>(`${this.apiUrl}/aianalysis/analyze-benchmark`, { name, data });
    }

    explainVoicing(name: string, data: any): Observable<{ explanation: string }> {
        return this.http.post<{ analysis?: string; explanation?: string }>(`${this.apiUrl}/aianalysis/explain-voicing`, { name, data })
            .pipe(map(res => ({ explanation: res.explanation || res.analysis || '' })));
    }

    executeCode(code: string): Observable<any> {
        return this.http.post<any>(`${this.apiUrl}/notebook/execute`, { code });
    }

    /**
     * Find similar chords by ID
     */
    findSimilar(id: number, limit: number = 20): Observable<ChordSearchResult[]> {
        const params = new HttpParams().set('limit', limit.toString());
        return this.http.get<ChordSearchResult[]>(`${this.apiUrl}/vectorsearch/similar/${id}`, { params })
            .pipe(
                catchError(err => {
                    console.error('Find similar failed:', err);
                    return of([]);
                })
            );
    }

    /**
     * Hybrid search with filters
     */
    hybridSearch(
        query: string,
        filters: { quality?: string; extension?: string; stackingType?: string; noteCount?: number },
        limit: number = 50
    ): Observable<ChordSearchResult[]> {
        let params = new HttpParams()
            .set('q', query)
            .set('limit', limit.toString());

        if (filters.quality) params = params.set('quality', filters.quality);
        if (filters.extension) params = params.set('extension', filters.extension);
        if (filters.stackingType) params = params.set('stackingType', filters.stackingType);
        if (filters.noteCount) params = params.set('noteCount', filters.noteCount.toString());

        return this.http.get<ChordSearchResult[]>(`${this.apiUrl}/vectorsearch/hybrid`, { params })
            .pipe(
                catchError(err => {
                    console.error('Hybrid search failed:', err);
                    return of([]);
                })
            );
    }

    /**
     * Load voicings for 3D visualization
     */
    loadVoicingsForVisualization(maxCount: number = 200): Observable<VoicingWithEmbedding[]> {
        const queries = [
            { query: 'major triads open position', quality: 'Major' },
            { query: 'minor chords jazz voicings', quality: 'Minor' },
            { query: 'dominant seventh blues', quality: 'Dominant' }
        ];

        return this.semanticSearch(queries[0].query, maxCount).pipe(
            map(results => this.transformToVoicingPoints(results))
        );
    }

    /**
     * Transform search results to visualization points
     */
    private transformToVoicingPoints(results: ChordSearchResult[]): VoicingWithEmbedding[] {
        return results.map((result, index) => {
            const chordData = (result.chordData || {}) as any;
            const quality = this.extractQuality(result.chordName, chordData);
            const extension = chordData['extension'] || '';

            const position = this.calculateHarmonicPosition(result, index, results.length);

            return {
                id: result.id,
                name: result.chordName,
                chordType: quality,
                quality,
                extension,
                position,
                harmonicAxis: HARMONIC_AXIS_LABELS[quality] || HARMONIC_AXIS_LABELS['default'],
                embedding: [position.x, position.y, position.z], // Simulating embedding for now
                frets: chordData['frets'] || undefined,
                difficulty: chordData['difficulty'] || undefined,
                cagedShape: chordData['cagedShape'] || undefined,
                intervalPattern: chordData['intervalPattern'] || undefined
            };
        });
    }

    /**
     * Extract quality from chord name or data
     */
    private extractQuality(chordName: string, chordData: Record<string, unknown>): string {
        if (chordData['quality']) return chordData['quality'] as string;

        const name = chordName.toLowerCase();

        if (name.includes('dim') || name.includes('°')) return 'Diminished';
        if (name.includes('aug') || name.includes('+')) return 'Augmented';
        if (name.includes('m7b5') || name.includes('ø')) return 'Half-Diminished';
        if (name.includes('maj7') || name.includes('Δ')) return 'Major7';
        if (name.includes('m7') || name.includes('min7')) return 'Minor7';
        if (name.includes('7') && !name.includes('maj')) return 'Dominant';
        if (name.includes('min') || name.includes('-')) return 'Minor';

        return 'Major';
    }

    /**
     * Calculate a 3D position for the chord based on harmonic properties
     */
    private calculateHarmonicPosition(
        result: ChordSearchResult,
        index: number,
        total: number
    ): { x: number; y: number; z: number } {
        const quality = this.extractQuality(result.chordName, result.chordData || {});

        const basePositions: Record<string, { x: number; y: number; z: number }> = {
            'Major': { x: 0.7, y: 0.5, z: 0.3 },
            'Minor': { x: 0.4, y: 0.3, z: 0.2 },
            'Dominant': { x: 0.2, y: -0.5, z: 0.4 },
            'Diminished': { x: -0.6, y: 0.2, z: -0.2 },
            'Augmented': { x: -0.3, y: -0.3, z: 0.7 },
            'Major7': { x: 0.5, y: 0.4, z: 0.5 },
            'Minor7': { x: 0.3, y: 0.2, z: 0.3 },
            'Half-Diminished': { x: -0.4, y: 0.1, z: -0.1 },
            'default': { x: 0, y: 0, z: 0 }
        };

        const base = basePositions[quality] || basePositions['default'];

        const angle = (index / Math.max(total, 1)) * Math.PI * 2;
        const radius = 0.15 + (result.similarityScore || 0) * 0.1;

        return {
            x: base.x + Math.cos(angle) * radius,
            y: base.y + Math.sin(angle) * radius * 0.5,
            z: base.z + Math.sin(angle * 0.5) * radius * 0.3
        };
    }

    /**
     * Get color for a chord type
     */
    static getColorForQuality(quality: string): string {
        return HARMONIC_AXIS_COLORS[quality] || HARMONIC_AXIS_COLORS['default'];
    }

    /**
     * Get harmonic axis label for a quality
     */
    static getAxisLabelForQuality(quality: string): string {
        return HARMONIC_AXIS_LABELS[quality] || HARMONIC_AXIS_LABELS['default'];
    }
}
