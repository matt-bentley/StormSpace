import { Component, OnInit } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';

import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Router } from '@angular/router';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { CreateBoardModalComponent } from './create-board-modal/create-board-modal.component';
import { SelectBoardModalComponent } from './select-board-modal/select-board-modal.component';

@Component({
    selector: 'app-splash',
    imports: [
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    FormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatDialogModule
],
    templateUrl: './splash.component.html',
    styleUrls: ['./splash.component.scss']
})
export class SplashComponent implements OnInit {

  public userName: string = '';

  constructor(
    private router: Router,
    private dialog: MatDialog) { }

  public createNewBoard(): void {
    if (this.userName) {
      localStorage.setItem('userName', this.userName);
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
      localStorage.setItem('userName', this.userName);
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
    const savedUserName = localStorage.getItem('userName');
    if (savedUserName) {
      this.userName = savedUserName;
    }
  }
}  