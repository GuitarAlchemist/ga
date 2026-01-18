import { Routes } from '@angular/router';
import { HomeComponent } from './home/home.component';
import { BenchmarkDetailComponent } from './benchmark-detail/benchmark-detail.component';
import { EmbeddingViewerComponent } from './embedding-viewer/embedding-viewer.component';
import { NotebookViewerComponent } from './notebook-viewer/notebook-viewer.component';
import { DocumentationViewerComponent } from './documentation-viewer/documentation-viewer.component';

export const routes: Routes = [
    { path: '', component: HomeComponent },
    { path: 'benchmark/:id', component: BenchmarkDetailComponent },
    { path: 'embeddings', component: EmbeddingViewerComponent },
    { path: 'notebook/:path', component: NotebookViewerComponent },
    { path: 'documentation/:path', component: DocumentationViewerComponent },
    { path: 'chat', loadComponent: () => import('./chat/chat.component').then(m => m.ChatComponent) },
    { path: '**', redirectTo: '' }
];
