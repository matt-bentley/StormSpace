import { Routes } from '@angular/router';
import { SplashComponent } from './splash/splash.component';
import { BoardComponent } from './board/board.component';
import { MsalGuard } from '@azure/msal-angular';

export const routes: Routes = [
  { path: '', component: SplashComponent },
  { path: 'boards/:id', component: BoardComponent },
  { path: '**', redirectTo: '', pathMatch: 'full' }
];

export const authRoutes: Routes = [
  { path: '', component: SplashComponent, canActivate: [MsalGuard] },
  { path: 'boards/:id', component: BoardComponent, canActivate: [MsalGuard] },
  { path: '**', redirectTo: '', pathMatch: 'full' }
];