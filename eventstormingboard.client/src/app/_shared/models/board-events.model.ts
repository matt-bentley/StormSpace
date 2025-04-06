import { Connection } from "./connection.model";
import { NoteMove } from "./note-move.model";
import { NoteSize } from "./note-size.model";
import { Note } from "./note.model";

export interface BoardEvent {
    boardId: string;
    isUndo?: boolean;
}

export interface UserJoinedBoardEvent extends BoardEvent {
    userName: string;
    connectionId: string;
}

export interface UserLeftBoardEvent extends BoardEvent {
    connectionId: string;
}

export interface BoardNameUpdatedEvent extends BoardEvent {
    newName: string;
    oldName: string;
}

export interface NoteCreatedEvent extends BoardEvent {
    note: Note;
}

export interface NoteTextEditedEvent extends BoardEvent {
    noteId: string;
    toText: string;
    fromText: string;
}

export interface NotesDeletedEvent extends BoardEvent {
    notes: Note[];
    connections: Connection[];
}

export interface NoteResizedEvent extends BoardEvent {
    noteId: string;
    from: NoteSize;
    to: NoteSize;
}

export interface NotesMovedEvent extends BoardEvent {
    from: NoteMove[];
    to: NoteMove[];
}

export interface ConnectionCreatedEvent extends BoardEvent {
    connection: Connection;
}

export interface PastedEvent extends BoardEvent {
    notes: Note[];
    connections: Connection[];
}