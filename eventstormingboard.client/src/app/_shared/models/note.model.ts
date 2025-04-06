import { NoteSize } from "./note-size.model";

export interface Note extends NoteSize {
    id: string;
    text: string;
    color: string;
    selected?: boolean;
} 