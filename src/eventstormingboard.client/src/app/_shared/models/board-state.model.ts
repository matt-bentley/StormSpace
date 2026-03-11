import { Connection } from "./connection.model";
import { Note } from "./note.model";

export interface BoardState {
    name: string;
    notes: Note[];
    connections: Connection[];
}