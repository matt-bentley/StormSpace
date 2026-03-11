export interface BoardDto {
  id: string;
  name: string;
  domain?: string;
  sessionScope?: string;
  agentInstructions?: string;
  notes: NoteDto[];
  connections: ConnectionDto[];
}

export interface BoardSummaryDto {
  id: string;
  name: string;
}

export interface BoardCreateDto {
  name: string;
  domain?: string;
  sessionScope?: string;
  agentInstructions?: string;
}

export interface NoteDto {
  id: string;
  text: string;
  x: number;
  y: number;
  width: number;
  height: number;
  color: string;
  type: string;
}

export interface ConnectionDto {
  fromNoteId: string;
  toNoteId: string;
}
