import { Component, Inject, OnInit } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { FormsModule } from '@angular/forms';
import { MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { CommonModule } from '@angular/common';
import { BoardsService } from '../../_shared/services/boards.service';
import { BoardSummaryDto } from '../../_shared/models/board.model';
import { MatSelectModule } from '@angular/material/select';

@Component({
  selector: 'app-select-board-modal',
  standalone: true,
  imports: [
    MatFormFieldModule,
    MatInputModule,
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    CommonModule,
    MatSelectModule
  ],
  templateUrl: './select-board-modal.component.html',
  styleUrls: ['./select-board-modal.component.scss']
})
export class SelectBoardModalComponent implements OnInit {
  boards: BoardSummaryDto[] = [];
  selectedBoardId: string | null = null;

  constructor(
    public dialogRef: MatDialogRef<SelectBoardModalComponent>,
    private boardsService: BoardsService,
    @Inject(MAT_DIALOG_DATA) public data: { id: string }
  ) { }

  ngOnInit(): void {
    this.boardsService.get().subscribe({
      next: (boards) => {
        this.boards = boards;
      },
      error: (err) => {
        console.error('Failed to fetch boards:', err);
      }
    });
  }

  onCancel(): void {
    this.dialogRef.close();
  }

  onSelect(): void {
    if (this.selectedBoardId) {
      this.dialogRef.close(this.selectedBoardId);
    }
  }
}