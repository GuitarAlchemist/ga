import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { ProgressionComponent, ChordEvent } from '../shared/progression.component';

@Component({
    selector: 'app-chat',
    standalone: true,
    imports: [CommonModule, FormsModule, ProgressionComponent],
    template: `
    <div class="chat-container">
      <div class="header">
        <h2>Guitar Alchemist AI</h2>
      </div>
      <div class="messages">
        <div *ngIf="messages.length === 0" class="welcome">
           <h3>Welcome!</h3>
           <p>Ask me to analyze chords, suggest voicings, or create progressions.</p>
           <div class="hints">
             <span (click)="setInput('Analyze this progression: Dm7 G7 Cmaj7')">Analyze ii-V-I</span>
             <span (click)="setInput('Suggest a shell voicing for C7')">Shell Voicings</span>
             <span (click)="setInput('Give me a tab for A minor')">Tab Generation</span>
           </div>
        </div>

        <div *ngFor="let msg of messages" class="message" [class.user]="msg.role === 'user'" [class.assistant]="msg.role === 'assistant'">
           <div class="role">{{ msg.role | titlecase }}</div>
           <div class="content">{{ msg.content }}</div>
           <div *ngIf="msg.progressionEvents && msg.progressionEvents.length > 0" class="attachment">
             <div class="attachment-label">Extracted Progression:</div>
             <app-progression [events]="msg.progressionEvents"></app-progression>
           </div>
        </div>
        <div *ngIf="isLoading" class="loading">
             <span class="dot">.</span><span class="dot">.</span><span class="dot">.</span>
        </div>
      </div>
      
      <div class="input-area">
        <input [(ngModel)]="input" (keyup.enter)="send()" [disabled]="isLoading" placeholder="Ask about chords (e.g. 'Analyze this progression: Dm7 G7 Cmaj7')">
        <button (click)="send()" [disabled]="isLoading || !input.trim()">Send</button>
      </div>
    </div>
  `,
    styles: [`
    .chat-container { display: flex; flex-direction: column; height: 90vh; max-width: 900px; margin: 0 auto; border: 1px solid #ddd; background: #fff; box-shadow: 0 0 20px rgba(0,0,0,0.05); }
    .header { padding: 15px 20px; border-bottom: 1px solid #eee; background: #f8f9fa; font-weight: bold; color: #2c3e50; }
    .messages { flex: 1; overflow-y: auto; padding: 20px; display: flex; flex-direction: column; gap: 20px; background: #fdfdfd; }
    .message { max-width: 85%; padding: 15px; border-radius: 12px; box-shadow: 0 1px 2px rgba(0,0,0,0.05); position: relative; }
    .message.user { align-self: flex-end; background: #e3f2fd; color: #1565c0; border-bottom-right-radius: 0; }
    .message.assistant { align-self: flex-start; background: #fff; border: 1px solid #eee; border-bottom-left-radius: 0; }
    
    .role { font-size: 0.75rem; color: #999; margin-bottom: 6px; text-transform: uppercase; letter-spacing: 0.5px; }
    .content { white-space: pre-wrap; line-height: 1.6; font-size: 1rem; }
    
    .attachment { margin-top: 15px; border-top: 1px dashed #ddd; padding-top: 10px; }
    .attachment-label { font-size: 0.8rem; color: #666; margin-bottom: 5px; font-weight: bold; }
    
    .loading { text-align: center; color: #888; padding: 10px; }
    .dot { animation: pulse 1.5s infinite; }
    .dot:nth-child(2) { animation-delay: 0.2s; }
    .dot:nth-child(3) { animation-delay: 0.4s; }
    
    @keyframes pulse { 0% { opacity: 0.2; } 50% { opacity: 1; } 100% { opacity: 0.2; } }

    .input-area { padding: 20px; background: #fff; border-top: 1px solid #ddd; display: flex; gap: 10px; align-items: center; }
    input { flex: 1; padding: 12px 15px; border: 1px solid #ccc; border-radius: 25px; font-size: 1rem; outline: none; transition: border 0.2s; }
    input:focus { border-color: #3498db; }
    button { padding: 10px 25px; background: #3498db; color: white; border: none; border-radius: 25px; cursor: pointer; font-weight: 600; transition: background 0.2s; }
    button:hover { background: #2980b9; }
    button:disabled { opacity: 0.6; cursor: not-allowed; background: #bdc3c7; }

    .welcome { text-align: center; margin-top: 50px; color: #7f8c8d; }
    .hints { display: flex; gap: 10px; justify-content: center; margin-top: 20px; flex-wrap: wrap; }
    .hints span { padding: 8px 15px; background: #ecf0f1; border-radius: 20px; font-size: 0.9rem; cursor: pointer; transition: background 0.2s; }
    .hints span:hover { background: #dfe6e9; color: #2980b9; }
  `]
})
export class ChatComponent {
    messages: any[] = [];
    input = '';
    isLoading = false;

    constructor(private http: HttpClient) { }

    setInput(text: string) {
        this.input = text;
    }

    send() {
        if (!this.input.trim()) return;

        this.messages.push({ role: 'user', content: this.input });
        const payload = { message: this.input };
        this.input = '';
        this.isLoading = true;

        this.http.post<any>('http://localhost:7001/api/chat', payload).subscribe({
            next: (res) => {
                this.isLoading = false;
                const botMsg: any = { role: 'assistant', content: res.naturalLanguageAnswer };

                if (res.progression) {
                    botMsg.progressionEvents = this.mapProgression(res.progression);
                }
                this.messages.push(botMsg);
            },
            error: (err) => {
                this.isLoading = false;
                console.error(err);
                this.messages.push({ role: 'assistant', content: 'Connection Error. Please ensure GA API is running on localhost:7001.' });
            }
        });
    }

    mapProgression(p: any): ChordEvent[] {
        return p.steps.map((s: any) => ({
            name: s.label,
            duration: s.durationMs,
            frets: this.extractFrets(s.label)
        }));
    }

    extractFrets(label: string): number[] {
        // Label "Dm7 (x-0-2-2-1-0)" or "A (x02220)"
        const match = label.match(/\((.*?)\)/);
        if (match && match[1]) {
            const diagram = match[1];
            let parts = [];
            if (diagram.includes('-')) {
                parts = diagram.split('-');
            } else {
                parts = diagram.split('');
            }

            if (parts.length === 6) {
                return parts.map(p => {
                    if (p.toLowerCase() === 'x') return -1;
                    const f = parseInt(p);
                    return isNaN(f) ? -1 : f;
                });
            }
        }
        return [-1, -1, -1, -1, -1, -1];
    }
}
