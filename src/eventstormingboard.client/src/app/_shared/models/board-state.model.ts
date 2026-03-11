import { Connection } from "./connection.model";
import { Note } from "./note.model";

export interface BoardState {
    name: string;
    domain?: string;
    sessionScope?: string;
    agentInstructions?: string;
    notes: Note[];
    connections: Connection[];
}