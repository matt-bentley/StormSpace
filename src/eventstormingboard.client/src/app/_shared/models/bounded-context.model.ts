export interface BoundedContext {
  id: string;
  name: string;
  x: number;
  y: number;
  width: number;
  height: number;
  color?: string;
  selected?: boolean;
}
