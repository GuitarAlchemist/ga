import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';

export interface Breadcrumb {
    label: string;
    path?: string;
}

@Component({
    selector: 'app-breadcrumb',
    standalone: true,
    imports: [CommonModule, RouterLink],
    template: `
    <nav class="breadcrumb">
      <span *ngFor="let crumb of crumbs; let last = last">
        <a *ngIf="crumb.path && !last" [routerLink]="crumb.path">{{ crumb.label }}</a>
        <span *ngIf="!crumb.path || last" class="current">{{ crumb.label }}</span>
        <span *ngIf="!last" class="separator">/</span>
      </span>
    </nav>
  `,
    styles: [`
    .breadcrumb { padding: 12px 24px; font-size: 0.9rem; background: rgba(13, 17, 23, 0.8); backdrop-filter: blur(8px); border-bottom: 1px solid #30363d; display: flex; align-items: center; }
    .breadcrumb a { color: #58a6ff; text-decoration: none; transition: color 0.2s; }
    .breadcrumb a:hover { color: #79c0ff; text-decoration: underline; }
    .breadcrumb .current { color: #c9d1d9; font-weight: 500; }
    .breadcrumb .separator { margin: 0 10px; color: #484f58; font-size: 1rem; }
  `]
})
export class BreadcrumbComponent {
    @Input() crumbs: Breadcrumb[] = [];
}
