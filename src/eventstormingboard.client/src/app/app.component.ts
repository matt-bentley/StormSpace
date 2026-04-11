import { Component, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, RouterOutlet } from '@angular/router';
import { IconsService } from './_shared/services/icons.service';
import { MatIconModule } from '@angular/material/icon';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTooltipModule } from '@angular/material/tooltip';
import { FormsModule } from '@angular/forms';
import { UserService } from './_shared/services/user.service';
import { ThemeService } from './_shared/services/theme.service';

@Component({
    selector: 'app-root',
    imports: [
        RouterOutlet,
        MatIconModule,
        MatSlideToggleModule,
        MatTooltipModule,
        FormsModule
    ],
    templateUrl: './app.component.html',
    styleUrl: './app.component.scss'
})
export class AppComponent {
  private iconsService = inject(IconsService);
  private router = inject(Router);
  private userService = inject(UserService);
  public readonly themeService = inject(ThemeService);

  public isUserMenuOpen = false;
  public isSettingsMenuOpen = false;
  public userName: string = '';

  constructor() {
    this.iconsService.registerIcons();
    this.userService.displayName$.pipe(takeUntilDestroyed()).subscribe(name => {
      this.userName = name;
    });
  }

  public get isNameReadOnly(): boolean {
    return this.userService.isReadOnly;
  }

  public home() {
    this.router.navigate(['/']);
  }

  public get userInitial(): string {
    return this.userName ? this.userName.charAt(0).toUpperCase() : '';
  }

  public toggleUserMenu(): void {
    this.isUserMenuOpen = !this.isUserMenuOpen;
  }

  public saveUserName(): void {
    this.userService.setDisplayName(this.userName);
    this.isUserMenuOpen = false;
  }

  public toggleSettingsMenu(): void {
    this.isSettingsMenuOpen = !this.isSettingsMenuOpen;
  }
}