import { AfterViewInit, Component, ElementRef, Inject, ViewChild } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { FormsModule } from '@angular/forms';
import { MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-note-text-modal',
  standalone: true,
  imports: [
    MatFormFieldModule,
    MatInputModule,
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    CommonModule
  ],
  templateUrl: './note-text-modal.component.html',
  styleUrls: ['./note-text-modal.component.scss']
})
export class NoteTextModalComponent implements AfterViewInit {

  @ViewChild('noteInput') 
  public noteInput!: ElementRef<HTMLInputElement>;

  constructor(
    public dialogRef: MatDialogRef<NoteTextModalComponent>,
    @Inject(MAT_DIALOG_DATA) public data: { text: string }
  ) {}

  public onCancel(): void {
    this.dialogRef.close();
  }

  public onSave(): void {
    this.dialogRef.close(this.data.text);
  }

  public ngAfterViewInit(): void {
    setTimeout(() => this.noteInput.nativeElement.focus(), 500);
  }
}