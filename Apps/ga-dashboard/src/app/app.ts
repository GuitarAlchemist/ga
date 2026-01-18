import { Component, signal, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { BreadcrumbComponent } from './shared/breadcrumb.component';
import { BreadcrumbService } from './services/navigation.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, BreadcrumbComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  private readonly navService = inject(BreadcrumbService);
  protected readonly title = signal('ga-dashboard');
  protected readonly crumbs = this.navService.crumbs;
}
