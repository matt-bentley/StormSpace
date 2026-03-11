import { NoteSize } from "./note-size.model";

export type NoteType =
  | 'event'
  | 'command'
  | 'aggregate'
  | 'user'
  | 'policy'
  | 'readModel'
  | 'externalSystem'
  | 'concern';

export interface Note extends NoteSize {
  id: string;
  text: string;
  color: string;
  type: NoteType;
  selected?: boolean;
}
