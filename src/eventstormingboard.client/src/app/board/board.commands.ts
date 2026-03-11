import { BoardState } from "../_shared/models/board-state.model";
import { Command } from "../_shared/models/command.model";
import { Connection } from "../_shared/models/connection.model";
import { NoteMove } from "../_shared/models/note-move.model";
import { NoteSize } from "../_shared/models/note-size.model";
import { Note } from "../_shared/models/note.model";

export class UpdateBoardNameCommand extends Command<BoardState> {

    constructor(private newName: string,
        private oldName: string) {
        super();
    }

    execute(){
        this.state.name = this.newName;
    }

    undo() {
        this.state.name = this.oldName;
    }
}

export class CreateNoteCommand extends Command<BoardState> {

    constructor(private note: Note) {
        super();
    }

    execute() {
        if (!this.state.notes.some(e => e.id === this.note.id)) {
            this.state.notes.push(this.note);
        }
    }

    undo() {
        const index = this.state.notes.findIndex(n => n.id === this.note.id);
        if (index !== -1) this.state.notes.splice(index, 1);
    }
}

export class EditNoteTextCommand extends Command<BoardState> {

    constructor(private noteId: string,
        private toText: string,
        private fromText: string) {
        super();
    }

    execute() {
        const note = this.state.notes.find(n => n.id === this.noteId);
        if (note) {
            note.text = this.toText;
        }
    }

    undo() {
        const note = this.state.notes.find(n => n.id === this.noteId);
        if (note) {
            note.text = this.fromText;
        }
    }
}

export class DeleteNotesCommand extends Command<BoardState> {

    constructor(private notes: Note[],
        private connections: Connection[]
    ) {
        super();
    }

    execute() {
        this.connections.forEach(connection => {
            const index = this.state.connections.findIndex(c =>
                c.fromNoteId === connection.fromNoteId &&
                c.toNoteId === connection.toNoteId);
            if (index !== -1) this.state.connections.splice(index, 1);
        });
        this.notes.forEach(note => {
            const index = this.state.notes.findIndex(n => n.id === note.id);
            if (index !== -1) this.state.notes.splice(index, 1);
        });
    }

    undo() {
        this.notes.forEach(note => {
            if (!this.state.notes.some(e => e.id === note.id)) {
                this.state.notes.push(note);
            }
        });
        this.connections.forEach(connection => {
            if (!this.state.connections.some(c =>
                c.fromNoteId === connection.fromNoteId &&
                c.toNoteId === connection.toNoteId)) {
                this.state.connections.push(connection);
            }
        });
    }
}

export class ResizeNoteCommand extends Command<BoardState> {

    constructor(private noteId: string,
        private from: NoteSize,
        private to: NoteSize) {
        super();
    }

    execute() {
        const note = this.state.notes.find(n => n.id === this.noteId);
        if (note) {
            note.x = this.to.x;
            note.y = this.to.y;
            note.width = this.to.width;
            note.height = this.to.height;
        }
    }

    undo() {
        const note = this.state.notes.find(n => n.id === this.noteId);
        if (note) {
            note.x = this.from.x;
            note.y = this.from.y;
            note.width = this.from.width;
            note.height = this.from.height;
        }
    }
}

export class MoveNotesCommand extends Command<BoardState> {
    constructor(
        public from: NoteMove[],
        public to: NoteMove[]
    ) {
        super();
    }

    execute() {
        this.to.forEach((noteMove) => {
            const note = this.state.notes.find(n => n.id === noteMove.noteId);
            if (note) {
                note.x = noteMove.coordinates.x;
                note.y = noteMove.coordinates.y;
            }
        });
    }

    undo() {
        this.from.forEach((noteMove) => {
            const note = this.state.notes.find(n => n.id === noteMove.noteId);
            if (note) {
                note.x = noteMove.coordinates.x;
                note.y = noteMove.coordinates.y;
            }
        });
    }
}

export class CreateConnectionCommand extends Command<BoardState> {

    constructor(private connection: Connection) {
        super();
    }

    execute() {
        if (!this.state.connections.some(e =>
            e.fromNoteId === this.connection.fromNoteId &&
            e.toNoteId === this.connection.toNoteId)) {
            this.state.connections.push(this.connection);
        }
    }

    undo() {
        const index = this.state.connections.findIndex(c =>
            c.fromNoteId === this.connection.fromNoteId &&
            c.toNoteId === this.connection.toNoteId);
        if (index !== -1) this.state.connections.splice(index, 1);
    }
}

export class PasteCommand extends Command<BoardState> {

    constructor(private notes: Note[], private connections: Connection[]) {
        super();
    }

    execute() {
        this.notes.forEach(note => {
            if (!this.state.notes.some(e => e.id === note.id)) {
                this.state.notes.push(note);
            }
        });
        this.connections.forEach(connection => {
            if (!this.state.connections.some(c =>
                c.fromNoteId === connection.fromNoteId &&
                c.toNoteId === connection.toNoteId)) {
                this.state.connections.push(connection);
            }
        });
        this.notes.forEach(n => n.selected = true);
    }

    undo() {
        this.connections.forEach(connection => {
            const index = this.state.connections.findIndex(c =>
                c.fromNoteId === connection.fromNoteId &&
                c.toNoteId === connection.toNoteId);
            if (index !== -1) this.state.connections.splice(index, 1);
        });
        this.notes.forEach(note => {
            const index = this.state.notes.findIndex(n => n.id === note.id);
            if (index !== -1) this.state.notes.splice(index, 1);
        });
    }
}