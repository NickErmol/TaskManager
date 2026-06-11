import { inject, Injectable, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { apiUrl } from '../http/api-base';
import { AuthStore } from '../auth';
import { TaskDto } from '../models';

export interface BoardRealtimeHandlers {
  onUpsert: (task: TaskDto, actorId: string) => void;
  onDelete: (taskId: string, actorId: string) => void;
  onReconnected: () => void;
}

/**
 * One SignalR connection to the board hub (spec §F3). The JWT travels via accessTokenFactory
 * (?access_token=) since browsers can't set headers on the WS handshake. Presence viewer ids
 * land in the `viewers` signal; task frames go to the injected handlers.
 */
@Injectable({ providedIn: 'root' })
export class BoardRealtimeService {
  private readonly auth = inject(AuthStore);
  private connection: HubConnection | null = null;
  private joinedBoardId: string | null = null;

  readonly viewers = signal<string[]>([]);

  isConnected(): boolean {
    return this.connection?.state === HubConnectionState.Connected;
  }

  async join(boardId: string, handlers: BoardRealtimeHandlers): Promise<void> {
    await this.leave();

    const connection = new HubConnectionBuilder()
      .withUrl(apiUrl('/hubs/board'), { accessTokenFactory: () => this.auth.accessToken() ?? '' })
      .withAutomaticReconnect()
      .build();

    connection.on('TaskUpserted', (task: TaskDto, actorId: string) => handlers.onUpsert(task, actorId));
    connection.on('TaskDeleted', (taskId: string, actorId: string) => handlers.onDelete(taskId, actorId));
    connection.on('PresenceChanged', (viewerIds: string[]) => this.viewers.set(viewerIds));
    // On reconnect, frames may have been missed while down — rejoin and let the caller refetch.
    connection.onreconnected(async () => {
      // Guard against a leave()/join() that swapped the active connection while we were down.
      if (this.connection !== connection) return;
      await connection.invoke('JoinBoard', boardId);
      handlers.onReconnected();
    });

    this.connection = connection;
    await connection.start();
    await connection.invoke('JoinBoard', boardId);
    this.joinedBoardId = boardId;
  }

  async leave(): Promise<void> {
    const connection = this.connection;
    const boardId = this.joinedBoardId;
    this.connection = null;
    this.joinedBoardId = null;
    this.viewers.set([]);
    if (connection && boardId && connection.state === HubConnectionState.Connected) {
      try {
        await connection.invoke('LeaveBoard', boardId);
      } catch {
        // best-effort; stopping the connection unwinds presence server-side anyway
      }
    }
    await connection?.stop();
  }
}
