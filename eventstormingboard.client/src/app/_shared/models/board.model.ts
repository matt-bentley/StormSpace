export interface BoardDto {
    id: string;
    name: string;
    notes: NoteDto[];
    connections: ConnectionDto[];
}

export interface BoardSummaryDto {
    id: string;
    name: string;
}

export interface BoardCreateDto {
    name: string;
}

export interface BoardUpdateDto {
    name: string;
    notes: NoteDto[];
    connections: ConnectionDto[];
}

export interface NoteDto {
    id: string;
    text: string;
    x: number;
    y: number;
    width: number;
    height: number;
    color: string;
}

export interface ConnectionDto {
    fromNoteId: string;
    toNoteId: string;
}