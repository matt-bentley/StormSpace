import { Component, inject } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { EVENT_STORMING_PHASES } from '../../_shared/models/board.model';

export interface BoardContextData {
  domain: string;
  sessionScope: string;
  phase: string;
  autonomousEnabled: boolean;
}

@Component({
  selector: 'app-board-context-modal',
  imports: [
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSlideToggleModule,
    FormsModule,
    MatDialogModule,
    MatButtonModule
  ],
  templateUrl: './board-context-modal.component.html',
  styleUrls: ['./board-context-modal.component.scss']
})
export class BoardContextModalComponent {
  public phases = EVENT_STORMING_PHASES;
  readonly dialogRef = inject(MatDialogRef<BoardContextModalComponent>);
  readonly data = inject<BoardContextData>(MAT_DIALOG_DATA);

  public onCancel(): void {
    this.dialogRef.close();
  }

  public onSave(): void {
    this.dialogRef.close(this.data);
  }
}
