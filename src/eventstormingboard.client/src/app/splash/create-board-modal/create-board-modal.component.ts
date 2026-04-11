import { Component, inject } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { FormsModule } from '@angular/forms';
import { MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';

import { BoardsService } from '../../_shared/services/boards.service';

@Component({
    selector: 'app-create-board-modal',
    imports: [
    MatFormFieldModule,
    MatInputModule,
    FormsModule,
    MatDialogModule,
    MatButtonModule
],
    templateUrl: './create-board-modal.component.html',
    styleUrls: ['./create-board-modal.component.scss']
})
export class CreateBoardModalComponent {
  readonly dialogRef = inject(MatDialogRef<CreateBoardModalComponent>);
  private boardsService = inject(BoardsService);
  readonly data = inject<{
    name: string;
    domain?: string;
    sessionScope?: string;
  }>(MAT_DIALOG_DATA);

  public onCancel(): void {
    this.dialogRef.close();
  }

  public onSave(): void {
    if (this.data.name) {
      this.boardsService.create({
        name: this.data.name,
        domain: this.data.domain?.trim() || undefined,
        sessionScope: this.data.sessionScope?.trim() || undefined
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