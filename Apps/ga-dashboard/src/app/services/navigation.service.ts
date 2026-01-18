import { Injectable, signal } from '@angular/core';
import { Breadcrumb } from '../shared/breadcrumb.component';

@Injectable({
    providedIn: 'root'
})
export class BreadcrumbService {
    private readonly _crumbs = signal<Breadcrumb[]>([]);

    readonly crumbs = this._crumbs.asReadonly();

    setCrumbs(crumbs: Breadcrumb[]) {
        this._crumbs.set(crumbs);
    }

    clear() {
        this._crumbs.set([]);
    }
}
