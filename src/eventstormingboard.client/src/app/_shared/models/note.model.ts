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
  type: NoteType;
  selected?: boolean;
}

const colors: { [key: string]: string } = {
  event: '#fdb634',
  command: '#61c4fd',
  aggregate: '#f8fb1d',
  user: '#ffffc5',
  policy: '#df89df',
  readModel: '#90f179',
  externalSystem: '#f5bee7',
  concern: '#f50532'
};

export function getNoteColor(type: string): string {
  return colors[type] || '#ffffff';
}
