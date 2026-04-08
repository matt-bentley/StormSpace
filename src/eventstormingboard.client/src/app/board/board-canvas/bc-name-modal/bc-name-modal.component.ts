import { Component, Inject } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { FormsModule } from '@angular/forms';
import { MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';


@Component({
    selector: 'app-bc-name-modal',
    imports: [
    MatFormFieldModule,
    MatInputModule,
    FormsModule,
    MatDialogModule,
    MatButtonModule
],
    templateUrl: './bc-name-modal.component.html',
    styleUrls: ['./bc-name-modal.component.scss']
})
export class BcNameModalComponent {

  constructor(
    public dialogRef: MatDialogRef<BcNameModalComponent>,
    @Inject(MAT_DIALOG_DATA) public data: { name: string }
  ) {}

  public onCancel(): void {
    this.dialogRef.close();
  }

  public onSave(): void {
    this.dialogRef.close(this.data.name);
  }
}
