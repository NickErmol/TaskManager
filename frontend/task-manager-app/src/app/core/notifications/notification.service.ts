import { inject, Injectable } from '@angular/core';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { apiUrl } from '../http/api-base';
import { NotificationDto } from '../models';
import { NotificationStore } from './notification.store';

/**
 * SignalR client for the notifications hub. Browsers cannot set Authorization
 * headers on the WebSocket handshake, so the JWT travels via accessTokenFactory
 * (?access_token= on the wire) per spec §4.4.
 */
@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly store = inject(NotificationStore);
  private connection: HubConnection | null = null;

  async connect(accessTokenFactory: () => string): Promise<void> {
    if (this.connection) return;

    this.connection = new HubConnectionBuilder()
      .withUrl(apiUrl('/hubs/notifications'), { accessTokenFactory })
      .withAutomaticReconnect()
      .build();

    this.connection.on('SendNotification', (notification: NotificationDto) =>
      this.store.receive(notification),
    );

    await this.connection.start();
  }

  async disconnect(): Promise<void> {
    const connection = this.connection;
    this.connection = null;
    await connection?.stop();
  }
}
