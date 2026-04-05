import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { BoardCreateDto, BoardDto, BoardSummaryDto } from '../models/board.model';
import { AgentConfiguration, AgentConfigurationCreate, AgentConfigurationUpdate, ToolDefinition } from '../models/agent-configuration.model';

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

  public delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  // Agent configuration CRUD

  public getAgents(boardId: string): Observable<AgentConfiguration[]> {
    return this.http.get<AgentConfiguration[]>(`${this.baseUrl}/${boardId}/agents`);
  }

  public addAgent(boardId: string, agent: AgentConfigurationCreate): Observable<AgentConfiguration> {
    return this.http.post<AgentConfiguration>(`${this.baseUrl}/${boardId}/agents`, agent);
  }

  public updateAgent(boardId: string, agentId: string, agent: AgentConfigurationUpdate): Observable<AgentConfiguration> {
    return this.http.put<AgentConfiguration>(`${this.baseUrl}/${boardId}/agents/${agentId}`, agent);
  }

  public deleteAgent(boardId: string, agentId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${boardId}/agents/${agentId}`);
  }

  public getAvailableTools(boardId: string): Observable<ToolDefinition[]> {
    return this.http.get<ToolDefinition[]>(`${this.baseUrl}/${boardId}/agents/available-tools`);
  }
}