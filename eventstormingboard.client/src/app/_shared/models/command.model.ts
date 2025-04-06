export interface ICommand<T> {
    initialize(value: T): void;
    execute(): void;
    undo(): void;
}

export abstract class Command<T> implements ICommand<T> {
    protected state!: T;

    constructor() {
        Object.defineProperty(this, 'state', {
            enumerable: false, // Prevents serialization
            writable: true,
            configurable: true
        });
    }

    initialize(value: T): void {
        this.state = value;
    }

    abstract execute(): void;
    abstract undo(): void;
}