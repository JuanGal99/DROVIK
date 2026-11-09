import { Injectable, inject, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { auditTime, filter, map, Observable, Subject } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from '../auth/auth.service';
import type { LiveState } from '../data/live-operations.service';

export type DataArea = 'shipments' | 'fleet' | 'incidents';
export type LiveConnection = 'connecting' | 'live' | 'reconnecting' | 'offline';

const ALL_AREAS: DataArea[] = ['shipments', 'fleet', 'incidents'];
// One finished route produces several events; screens should read once for all of them.
const CHANGE_WINDOW_MS = 400;
const RETRY_DELAYS_MS = [0, 2000, 5000, 10000];
const RETRY_LIMIT_DELAY_MS = 15000;

/** The session's single connection to the server: live positions for the map and change hints for every other screen. */
@Injectable({ providedIn: 'root' })
export class OperationsHubService {
  private readonly auth = inject(AuthService);
  private hub: HubConnection | null = null;
  private retry: ReturnType<typeof setTimeout> | null = null;
  private readonly changed = new Subject<DataArea>();
  private readonly stateUpdates = new Subject<LiveState>();
  private readonly reconnections = new Subject<void>();

  readonly connection = signal<LiveConnection>('connecting');
  readonly states: Observable<LiveState> = this.stateUpdates;
  readonly reconnected: Observable<void> = this.reconnections;

  /** Emits at most once per window, however many changes arrive in it. */
  changes(...areas: DataArea[]): Observable<void> {
    return this.changed.pipe(filter((area) => areas.includes(area)), auditTime(CHANGE_WINDOW_MS), map(() => undefined));
  }

  notifyChanged(area: DataArea): void { this.changed.next(area); }

  start(): void {
    if (this.hub) return;
    const hub = new HubConnectionBuilder()
      .withUrl(`${environment.gatewayUrl}/hubs/operations`, { accessTokenFactory: () => this.auth.freshToken() })
      .withAutomaticReconnect({ nextRetryDelayInMilliseconds: (context) => RETRY_DELAYS_MS[context.previousRetryCount] ?? RETRY_LIMIT_DELAY_MS })
      .configureLogging(LogLevel.None)
      .build();
    hub.on('state', (state: LiveState) => this.stateUpdates.next(state));
    hub.on('changed', (area: DataArea) => this.notifyChanged(area));
    hub.onreconnecting(() => this.connection.set('reconnecting'));
    hub.onreconnected(() => this.announceRecovery());
    hub.onclose(() => this.connection.set('offline'));
    this.hub = hub;
    this.open(hub, false);
  }

  stop(): void {
    if (this.retry) clearTimeout(this.retry);
    this.retry = null;
    void this.hub?.stop();
    this.hub = null;
  }

  private open(hub: HubConnection, retrying: boolean): void {
    this.connection.set('connecting');
    hub.start().then(() => {
      if (retrying) this.announceRecovery();
      else this.connection.set('live');
    }).catch(() => {
      if (this.hub !== hub) return;
      this.connection.set('offline');
      this.retry = setTimeout(() => this.open(hub, true), RETRY_LIMIT_DELAY_MS);
    });
  }

  /** Hints sent while disconnected are gone, so every screen has to read again. */
  private announceRecovery(): void {
    this.connection.set('live');
    this.reconnections.next();
    ALL_AREAS.forEach((area) => this.notifyChanged(area));
  }
}
