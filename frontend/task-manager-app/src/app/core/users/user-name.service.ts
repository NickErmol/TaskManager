import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { UsersApiService } from '../http/users-api.service';

/**
 * Memoizing id → display-name resolver over Identity's GET /api/users/{id}. One in-flight
 * request per id; resolved names are cached for the session. Keeps the activity feed (and
 * any future name display) from issuing N duplicate lookups.
 */
@Injectable({ providedIn: 'root' })
export class UserNameService {
  private readonly usersApi = inject(UsersApiService);
  private readonly cache = new Map<string, Promise<string>>();

  resolve(userId: string): Promise<string> {
    const hit = this.cache.get(userId);
    if (hit) return hit;
    const pending = firstValueFrom(this.usersApi.getById(userId))
      .then((u) => u.displayName)
      .catch(() => 'Someone'); // a deleted/unknown user still renders
    this.cache.set(userId, pending);
    return pending;
  }
}
