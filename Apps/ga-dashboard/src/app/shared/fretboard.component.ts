import { Component, Input, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
    selector: 'app-fretboard',
    standalone: true,
    imports: [CommonModule],
    template: `
    <div class="fretboard-container">
      <h3>{{ title }}</h3>
      <svg [attr.width]="width" [attr.height]="height" [attr.viewBox]="viewBox">
        <!-- Nut (if starting at 0) -->
        <rect *ngIf="startFret === 0" x="20" y="20" width="10" [attr.height]="neckHeight" fill="#333" />
        
        <!-- Frets -->
        <g *ngFor="let f of fretsToRender; let i = index">
           <line [attr.x1]="getFretX(i)" y1="20" [attr.x2]="getFretX(i)" [attr.y2]="20 + neckHeight" stroke="#888" stroke-width="2" />
           <text *ngIf="i > 0 && (i + startFret) % 2 !== 0" [attr.x]="getFretX(i) - (fretSpacing/2)" [attr.y]="height - 5" text-anchor="middle" font-size="10" fill="#666">{{ i + startFret }}</text>
        </g>

        <!-- Strings -->
        <g *ngFor="let s of strings; let i = index">
           <line x1="20" [attr.y1]="getStringY(i)" [attr.x2]="width - 20" [attr.y2]="getStringY(i)" stroke="#666" [attr.stroke-width]="i + 1" />
        </g>

        <!-- Dots -->
        <g *ngFor="let fret of dots">
           <!-- fret is string[] of frets: [E, A, D, G, B, e] -->
           <!-- index 0 is Low E (bottom visually if we want standard tab view, or top if looking at neck) -->
           <!-- Let's assume index 0 = Low E = Top of diagram -->
           <ng-container *ngFor="let f of fret.val; let stringIdx = index">
              <circle *ngIf="f >= 0" 
                      [attr.cx]="getDotX(f)" 
                      [attr.cy]="getStringY(stringIdx)" 
                      r="6" 
                      fill="#3498db" 
                      stroke="#fff" 
                      stroke-width="2" />
              <text *ngIf="f === -1" 
                    [attr.x]="10" 
                    [attr.y]="getStringY(stringIdx) + 4" 
                    font-size="12" 
                    fill="#e74c3c">×</text>
           </ng-container>
        </g>
      </svg>
    </div>
  `,
    styles: [`
    .fretboard-container { border: 1px solid #eee; border-radius: 8px; padding: 16px; background: #fff; display: inline-block; }
    h3 { margin-top: 0; margin-bottom: 12px; font-size: 1rem; color: #333; }
  `]
})
export class FretboardComponent implements OnChanges {
    @Input() frets: number[] = []; // [LowE, A, D, G, B, HighE] e.g. [-1, 3, 2, 0, 1, 0]
    @Input() title: string = 'Fretboard';
    @Input() startFret: number = 0;

    width = 300;
    height = 150;
    neckHeight = 100;
    fretSpacing = 40;
    stringSpacing = 16;

    strings = [0, 1, 2, 3, 4, 5]; // 6 strings
    fretsToRender: number[] = [];
    dots: { val: number[] }[] = [];

    get viewBox() {
        return `0 0 ${this.width} ${this.height}`;
    }

    ngOnChanges(changes: SimpleChanges) {
        if (changes['frets']) {
            this.render();
        }
    }

    render() {
        // Logic to determine range
        const validFrets = this.frets.filter(f => f > 0);
        const minFret = validFrets.length > 0 ? Math.min(...validFrets) : 0;
        const maxFret = validFrets.length > 0 ? Math.max(...validFrets) : 4;

        this.startFret = Math.max(0, minFret - 1);
        const endFret = Math.max(this.startFret + 5, maxFret + 1);

        this.fretsToRender = Array.from({ length: endFret - this.startFret + 1 }, (_, i) => i);
        this.width = this.fretsToRender.length * this.fretSpacing + 60;

        this.dots = [{ val: this.frets }];
    }

    getFretX(i: number): number {
        return 30 + (i * this.fretSpacing);
    }

    getStringY(i: number): number {
        // 0 = Low E (Top), 5 = High E (Bottom)
        // Visual preference: Thick string on bottom? Usually diagram has Low E on bottom visually?
        // Standard chord charts: Top line is High E, Bottom line is Low E. 
        // Let's stick to: Top string (i=0) is Low E (Thick).
        return 30 + (i * this.stringSpacing);
    }

    getDotX(fret: number): number {
        if (fret === 0) return 15; // Open string nut position
        const relativeFret = fret - this.startFret;
        // Center between fret lines i-1 and i
        // Line index for fret F is F - startFret
        // We want center of (relativeFret-1) and relativeFret
        return 30 + ((relativeFret - 0.5) * this.fretSpacing);
    }
}
