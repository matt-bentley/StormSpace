import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';

type ShortcutSection = {
  title: string;
  items: { keys: string[]; description: string }[];
};

@Component({
  selector: 'app-keyboard-shortcuts-modal',
  imports: [CommonModule, MatDialogModule, MatButtonModule],
  templateUrl: './keyboard-shortcuts-modal.component.html',
  styleUrls: ['./keyboard-shortcuts-modal.component.scss']
})
export class KeyboardShortcutsModalComponent {
  public readonly sections: ShortcutSection[] = [
    {
      title: 'Editing',
      items: [
        { keys: ['Ctrl', 'Z'], description: 'Undo' },
        { keys: ['Ctrl', 'Y'], description: 'Redo' },
        { keys: ['Ctrl', 'Shift', 'Z'], description: 'Redo (alternative)' },
        { keys: ['Delete'], description: 'Delete selected notes and selected or linked connections' },
        { keys: ['Backspace'], description: 'Delete selected notes and selected or linked connections' },
        { keys: ['Ctrl', 'C'], description: 'Copy selected notes and their internal connections' },
        { keys: ['Ctrl', 'V'], description: 'Paste copied notes at cursor position' }
      ]
    },
    {
      title: 'Navigation',
      items: [
        { keys: ['Mouse Wheel'], description: 'Pan board' },
        { keys: ['Ctrl', 'Mouse Wheel'], description: 'Zoom in or out' },
        { keys: ['Middle Mouse Button + Drag'], description: 'Pan board' }
      ]
    },
    {
      title: 'Interaction',
      items: [
        { keys: ['Double Click'], description: 'Edit note text' },
        { keys: ['?'], description: 'Open this shortcuts guide' },
        { keys: ['Esc'], description: 'Close this guide' }
      ]
    }
  ];

  constructor(public dialogRef: MatDialogRef<KeyboardShortcutsModalComponent>) {}
}