export class BoardUser {
    constructor(boardId: string, userName: string, connectionId: string) {
        this.boardId = boardId;
        this.userName = userName;
        this.connectionId = connectionId;
    }
    boardId: string;
    userName: string;
    connectionId: string;

    getColour(): string {
        const colors = [
            '#FF5733', '#61c773', '#3357FF', '#FF33A1', '#A133FF', '#3dd3cc',
            '#e7b100', '#C70039', '#900C3F', '#581845', '#72ab0c'
        ];
        const hash = BoardUser.getUserNameHashcode(this.userName);
        const index = Math.abs(hash) % colors.length;
        return colors[index];
    }

    private static getUserNameHashcode(userName: string): number {
        let hash = 0;
        for (let i = 0; i < userName.length; i++) {
            hash = userName.charCodeAt(i) + ((hash << 5) - hash);
        }
        return hash;
    }
}