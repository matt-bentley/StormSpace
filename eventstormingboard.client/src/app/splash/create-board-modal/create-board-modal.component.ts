import { Component, Inject } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { FormsModule } from '@angular/forms';
import { MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { CommonModule } from '@angular/common';
import { BoardsService } from '../../_shared/services/boards.service';

@Component({
  selector: 'app-create-board-modal',
  standalone: true,
  imports: [
    MatFormFieldModule,
    MatInputModule,
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    CommonModule
  ],
  templateUrl: './create-board-modal.component.html',
  styleUrls: ['./create-board-modal.component.scss']
})
export class CreateBoardModalComponent {
  constructor(
    public dialogRef: MatDialogRef<CreateBoardModalComponent>,
    private boardsService: BoardsService,
    @Inject(MAT_DIALOG_DATA) public data: { name: string }
  ) { }

  public onCancel(): void {
    this.dialogRef.close();
  }

  public onSave(): void {
    if (this.data.name) {
      this.boardsService.create({
        name: this.data.name
      }).subscribe({
        next: (createdBoard) => {
          this.dialogRef.close(createdBoard.id);
        },
        error: (err) => {
          console.error('Failed to create board:', err);
        }
      });
    }
  }
}