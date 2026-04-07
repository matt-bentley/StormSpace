import { Component, OnDestroy, OnInit } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { IconsService } from './_shared/services/icons.service';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { FormsModule } from '@angular/forms';
import { UserService } from './_shared/services/user.service';
import { Subject, takeUntil } from 'rxjs';

@Component({
    selector: 'app-root',
    imports: [
        RouterOutlet,
        MatIconModule,
        MatTooltipModule,
        FormsModule
    ],
    templateUrl: './app.component.html',
    styleUrl: './app.component.scss'
})
export class AppComponent implements OnInit, OnDestroy {
  public isUserMenuOpen = false;
  public userName: string = '';
  private destroy$ = new Subject<void>();

  constructor(private iconsService: IconsService,
    private router: Router,
    private userService: UserService
  ) {
    this.iconsService.registerIcons();
  }

  public ngOnInit(): void {
    this.userService.displayName$.pipe(takeUntil(this.destroy$)).subscribe(name => {
      this.userName = name;
    });
  }

  public ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
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
}