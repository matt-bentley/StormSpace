import { Component, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { CreateBoardModalComponent } from './create-board-modal/create-board-modal.component';
import { SelectBoardModalComponent } from './select-board-modal/select-board-modal.component';
import { UserService } from '../_shared/services/user.service';

@Component({
    selector: 'app-splash',
    imports: [
    MatIconModule,
    FormsModule,
    MatDialogModule
],
    templateUrl: './splash.component.html',
    styleUrls: ['./splash.component.scss']
})
export class SplashComponent {
  private router = inject(Router);
  private dialog = inject(MatDialog);
  private userService = inject(UserService);

  public userName: string = '';

  constructor() {
    this.userService.displayName$.pipe(takeUntilDestroyed()).subscribe(name => {
      this.userName = name;
    });
  }

  public get isNameReadOnly(): boolean {
    return this.userService.isReadOnly;
  }

  public onUserNameChanged(): void {
    this.userService.setDisplayName(this.userName);
  }

  public createNewBoard(): void {
    if (this.userName) {
      this.userService.setDisplayName(this.userName);
      const dialogRef = this.dialog.open(CreateBoardModalComponent, {
        width: '500px',
        data: { name: '' }
      });

      dialogRef.afterClosed().subscribe((id: string | undefined) => {
        if (id) {
          this.router.navigateByUrl(`/boards/${id}`);
        }
      });
    }
  }

  public selectExistingBoard(): void {
    if (this.userName) {
      this.userService.setDisplayName(this.userName);
      const dialogRef = this.dialog.open(SelectBoardModalComponent, {
        width: '400px',
        data: { id: '' }
      });

      dialogRef.afterClosed().subscribe((id: string | undefined) => {
        if (id) {
          this.router.navigateByUrl(`/boards/${id}`);
        }
      });
    }
  }

}  