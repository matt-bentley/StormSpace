import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { BoardCreateDto, BoardDto, BoardSummaryDto, BoardUpdateDto } from '../models/board.model';

@Injectable({
  providedIn: 'root'
})
export class BoardsService {
  private baseUrl = '/api/boards';

  constructor(private http: HttpClient) {}

  public get(): Observable<BoardSummaryDto[]> {
    return this.http.get<BoardSummaryDto[]>(this.baseUrl);
  }

  public getById(id: string): Observable<BoardDto> {
    return this.http.get<BoardDto>(`${this.baseUrl}/${id}`);
  }

  public create(boardCreateDto: BoardCreateDto): Observable<BoardDto> {
    return this.http.post<BoardDto>(this.baseUrl, boardCreateDto);
  }

  public update(id: string, boardUpdateDto: BoardUpdateDto): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}`, boardUpdateDto);
  }
}