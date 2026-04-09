import { Component, OnDestroy, OnInit } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { CreateBoardModalComponent } from './create-board-modal/create-board-modal.component';
import { SelectBoardModalComponent } from './select-board-modal/select-board-modal.component';
import { UserService } from '../_shared/services/user.service';
import { Subject, takeUntil } from 'rxjs';

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
export class SplashComponent implements OnInit, OnDestroy {

  public userName: string = '';
  private destroy$ = new Subject<void>();

  constructor(
    private router: Router,
    private dialog: MatDialog,
    private userService: UserService) { }

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

  public ngOnInit(): void {
    this.userService.displayName$.pipe(takeUntil(this.destroy$)).subscribe(name => {
      this.userName = name;
    });
  }

  public ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}  