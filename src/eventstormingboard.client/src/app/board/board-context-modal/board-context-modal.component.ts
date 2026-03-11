import { Component, Inject } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';

export interface BoardContextData {
  domain: string;
  sessionScope: string;
  agentInstructions: string;
}

@Component({
  selector: 'app-board-context-modal',
  imports: [
    MatFormFieldModule,
    MatInputModule,
    FormsModule,
    MatDialogModule,
    MatButtonModule
  ],
  templateUrl: './board-context-modal.component.html',
  styleUrls: ['./board-context-modal.component.scss']
})
export class BoardContextModalComponent {
  constructor(
    public dialogRef: MatDialogRef<BoardContextModalComponent>,
    @Inject(MAT_DIALOG_DATA) public data: BoardContextData
  ) { }

  public onCancel(): void {
    this.dialogRef.close();
  }

  public onSave(): void {
    this.dialogRef.close(this.data);
  }
}
