import { BoardState } from "../_shared/models/board-state.model";
import { BoundedContext } from "../_shared/models/bounded-context.model";
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

export class UpdateBoardContextCommand extends Command<BoardState> {

    constructor(
        private newDomain: string | undefined,
        private oldDomain: string | undefined,
        private newSessionScope: string | undefined,
        private oldSessionScope: string | undefined,
        private newPhase: string | undefined,
        private oldPhase: string | undefined,
        private newAutonomousEnabled: boolean,
        private oldAutonomousEnabled: boolean
    ) {
        super();
    }

    execute() {
        this.state.domain = this.newDomain;
        this.state.sessionScope = this.newSessionScope;
        this.state.phase = this.newPhase;
        this.state.autonomousEnabled = this.newAutonomousEnabled;
    }

    undo() {
        this.state.domain = this.oldDomain;
        this.state.sessionScope = this.oldSessionScope;
        this.state.phase = this.oldPhase;
        this.state.autonomousEnabled = this.oldAutonomousEnabled;
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

export class CreateBoundedContextCommand extends Command<BoardState> {

    constructor(public boundedContext: BoundedContext) {
        super();
    }

    execute() {
        if (!this.state.boundedContexts.some(bc => bc.id === this.boundedContext.id)) {
            this.state.boundedContexts.push(this.boundedContext);
        }
    }

    undo() {
        const index = this.state.boundedContexts.findIndex(bc => bc.id === this.boundedContext.id);
        if (index !== -1) this.state.boundedContexts.splice(index, 1);
    }
}

export class UpdateBoundedContextCommand extends Command<BoardState> {

    constructor(
        public boundedContextId: string,
        public oldName: string | undefined,
        public newName: string | undefined,
        public oldX: number | undefined,
        public newX: number | undefined,
        public oldY: number | undefined,
        public newY: number | undefined,
        public oldWidth: number | undefined,
        public newWidth: number | undefined,
        public oldHeight: number | undefined,
        public newHeight: number | undefined
    ) {
        super();
    }

    execute() {
        const bc = this.state.boundedContexts.find(bc => bc.id === this.boundedContextId);
        if (bc) {
            if (this.newName !== undefined) bc.name = this.newName;
            if (this.newX !== undefined) bc.x = this.newX;
            if (this.newY !== undefined) bc.y = this.newY;
            if (this.newWidth !== undefined) bc.width = this.newWidth;
            if (this.newHeight !== undefined) bc.height = this.newHeight;
        }
    }

    undo() {
        const bc = this.state.boundedContexts.find(bc => bc.id === this.boundedContextId);
        if (bc) {
            if (this.oldName !== undefined) bc.name = this.oldName;
            if (this.oldX !== undefined) bc.x = this.oldX;
            if (this.oldY !== undefined) bc.y = this.oldY;
            if (this.oldWidth !== undefined) bc.width = this.oldWidth;
            if (this.oldHeight !== undefined) bc.height = this.oldHeight;
        }
    }
}

export class DeleteBoundedContextCommand extends Command<BoardState> {

    constructor(public boundedContext: BoundedContext) {
        super();
    }

    execute() {
        const index = this.state.boundedContexts.findIndex(bc => bc.id === this.boundedContext.id);
        if (index !== -1) this.state.boundedContexts.splice(index, 1);
    }

    undo() {
        if (!this.state.boundedContexts.some(bc => bc.id === this.boundedContext.id)) {
            this.state.boundedContexts.push(this.boundedContext);
        }
    }
}

export class MoveBoundedContextCommand extends Command<BoardState> {

    constructor(
        public boundedContextId: string,
        public oldX: number,
        public newX: number,
        public oldY: number,
        public newY: number
    ) {
        super();
    }

    execute() {
        const bc = this.state.boundedContexts.find(bc => bc.id === this.boundedContextId);
        if (bc) {
            bc.x = this.newX;
            bc.y = this.newY;
        }
    }

    undo() {
        const bc = this.state.boundedContexts.find(bc => bc.id === this.boundedContextId);
        if (bc) {
            bc.x = this.oldX;
            bc.y = this.oldY;
        }
    }
}

export class ResizeBoundedContextCommand extends Command<BoardState> {

    constructor(
        public boundedContextId: string,
        public oldX: number,
        public newX: number,
        public oldY: number,
        public newY: number,
        public oldWidth: number,
        public newWidth: number,
        public oldHeight: number,
        public newHeight: number
    ) {
        super();
    }

    execute() {
        const bc = this.state.boundedContexts.find(bc => bc.id === this.boundedContextId);
        if (bc) {
            bc.x = this.newX;
            bc.y = this.newY;
            bc.width = this.newWidth;
            bc.height = this.newHeight;
        }
    }

    undo() {
        const bc = this.state.boundedContexts.find(bc => bc.id === this.boundedContextId);
        if (bc) {
            bc.x = this.oldX;
            bc.y = this.oldY;
            bc.width = this.oldWidth;
            bc.height = this.oldHeight;
        }
    }
}