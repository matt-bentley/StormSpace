import { Routes } from '@angular/router';
import { SplashComponent } from './splash/splash.component';
import { BoardComponent } from './board/board.component';

export const routes: Routes = [
  { path: '', component: SplashComponent },
  { path: 'boards/:id', component: BoardComponent },
  { path: '**', redirectTo: '', pathMatch: 'full' }
];