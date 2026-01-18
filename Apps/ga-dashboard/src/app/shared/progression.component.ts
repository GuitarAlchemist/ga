import { Component, Input, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FretboardComponent } from './fretboard.component';

export interface ChordEvent {
    frets: number[];
    duration?: number; // in ms
    name?: string;
}

@Component({
    selector: 'app-progression',
    standalone: true,
    imports: [CommonModule, FretboardComponent],
    template: `
    <div class="progression-container">
      <div class="controls">
        <button class="play-btn" (click)="play()" [disabled]="isPlaying">{{ isPlaying ? 'Playing...' : '▶ Play Progression' }}</button>
        <button class="stop-btn" (click)="stop()" [disabled]="!isPlaying">⏹ Stop</button>
      </div>

      <div class="timeline">
        <div *ngFor="let chord of events; let i = index" 
             class="chord-item" 
             [class.active]="currentStep === i"
             (click)="playChord(chord)">
             
             <div class="chord-name">{{ chord.name || 'Chord ' + (i+1) }}</div>
             <app-fretboard [frets]="chord.frets" [title]="''" [startFret]="0"></app-fretboard>
             
        </div>
      </div>
    </div>
  `,
    styles: [`
    .progression-container { padding: 16px; background: #fff; border-radius: 8px; border: 1px solid #eee; overflow-x: auto; }
    .controls { margin-bottom: 16px; display: flex; gap: 8px; }
    
    .timeline { display: flex; gap: 16px; padding-bottom: 8px; }
    
    .chord-item { 
        border: 2px solid transparent; 
        border-radius: 8px; 
        padding: 8px; 
        cursor: pointer; 
        transition: all 0.2s;
        opacity: 0.7;
        transform: scale(0.95);
    }
    .chord-item:hover { opacity: 1; transform: scale(1); background: #f8f9fa; }
    .chord-item.active { 
        border-color: #3498db; 
        opacity: 1; 
        transform: scale(1.05); 
        box-shadow: 0 4px 12px rgba(52, 152, 219, 0.2);
    }

    .chord-name { text-align: center; font-weight: bold; margin-bottom: 4px; font-size: 0.9rem; }
    
    button { padding: 8px 16px; border-radius: 4px; border: none; cursor: pointer; font-weight: 600; }
    .play-btn { background: #2ecc71; color: white; }
    .stop-btn { background: #e74c3c; color: white; }
    button:disabled { opacity: 0.5; cursor: not-allowed; }
  `]
})
export class ProgressionComponent implements OnDestroy {
    @Input() events: ChordEvent[] = [];

    isPlaying = false;
    currentStep = -1;
    private audioCtx: AudioContext | null = null;
    private timeouts: any[] = [];

    // MIDI Base numbers for Standard E Tuning (E2, A2, D3, G3, B3, E4)
    private readonly STRING_BASES = [40, 45, 50, 55, 59, 64];

    ngOnDestroy() {
        this.stop();
        if (this.audioCtx) {
            this.audioCtx.close();
        }
    }

    async play() {
        if (this.isPlaying) return;
        this.isPlaying = true;
        this.currentStep = -1;

        if (!this.audioCtx) {
            this.audioCtx = new (window.AudioContext || (window as any).webkitAudioContext)();
        }

        let accumulatedTime = 0;

        // Schedule playback
        this.events.forEach((chord, index) => {
            const duration = chord.duration || 1000;

            // Schedule visual update
            const t = setTimeout(() => {
                this.currentStep = index;
                this.playChordAudio(chord);
            }, accumulatedTime);

            this.timeouts.push(t);
            accumulatedTime += duration;
        });

        // Cleanup at end
        const endT = setTimeout(() => {
            this.isPlaying = false;
            this.currentStep = -1;
            this.clearTimeouts();
        }, accumulatedTime);
        this.timeouts.push(endT);
    }

    stop() {
        this.isPlaying = false;
        this.currentStep = -1;
        this.clearTimeouts();
    }

    playChord(chord: ChordEvent) {
        if (!this.audioCtx) {
            this.audioCtx = new (window.AudioContext || (window as any).webkitAudioContext)();
        }
        this.playChordAudio(chord);
    }

    private playChordAudio(chord: ChordEvent) {
        if (!this.audioCtx) return;

        const now = this.audioCtx.currentTime;
        const duration = (chord.duration || 1000) / 1000;

        // Strumming effect: 30ms delay between strings
        chord.frets.forEach((fret, stringIdx) => {
            if (fret >= 0) { // If not muted (-1)
                const midiNote = this.STRING_BASES[stringIdx] + fret;
                const frequency = 440 * Math.pow(2, (midiNote - 69) / 12);
                const strumDelay = stringIdx * 0.03; // 30ms

                this.playTone(frequency, now + strumDelay, duration);
            }
        });
    }

    private playTone(freq: number, startTime: number, duration: number) {
        if (!this.audioCtx) return;

        const osc = this.audioCtx.createOscillator();
        const gain = this.audioCtx.createGain();

        osc.type = 'triangle'; // Guitar-ish?
        osc.frequency.value = freq;

        // Envelope
        gain.gain.setValueAtTime(0, startTime);
        gain.gain.linearRampToValueAtTime(0.3, startTime + 0.05); // Attack
        gain.gain.exponentialRampToValueAtTime(0.001, startTime + duration); // Decay

        osc.connect(gain);
        gain.connect(this.audioCtx.destination);

        osc.start(startTime);
        osc.stop(startTime + duration);
    }

    private clearTimeouts() {
        this.timeouts.forEach(t => clearTimeout(t));
        this.timeouts = [];
    }
}
