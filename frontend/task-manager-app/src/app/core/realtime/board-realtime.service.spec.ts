import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { BoardRealtimeService } from './board-realtime.service';

describe('BoardRealtimeService', () => {
  let service: BoardRealtimeService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    service = TestBed.inject(BoardRealtimeService);
  });

  it('is created and starts disconnected', () => {
    expect(service).toBeTruthy();
    expect(service.isConnected()).toBe(false);
  });

  it('exposes a viewers signal defaulting to empty', () => {
    expect(service.viewers()).toEqual([]);
  });
});
