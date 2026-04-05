import { Connection } from "./connection.model";
import { Note } from "./note.model";

export interface BoardState {
    name: string;
    domain?: string;
    sessionScope?: string;
    phase?: string;
    autonomousEnabled: boolean;
    notes: Note[];
    connections: Connection[];
}