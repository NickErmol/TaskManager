import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { apiUrl } from './api-base';
import { UserDto } from '../models';

@Injectable({ providedIn: 'root' })
export class UsersApiService {
  private readonly http = inject(HttpClient);

  getById(id: string): Observable<UserDto> {
    return this.http.get<UserDto>(apiUrl(`/api/users/${id}`));
  }

  search(query: string): Observable<UserDto[]> {
    const params = new HttpParams().set('q', query);
    return this.http.get<UserDto[]>(apiUrl('/api/users/search'), { params });
  }
}
