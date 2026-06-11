import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { BoardsStore } from './boards.store';
import { EMPTY_FILTER } from './board-filter';

describe('BoardsStore filter state', () => {
  let store: InstanceType<typeof BoardsStore>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [MatSnackBarModule],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideNoopAnimations()],
    });
    store = TestBed.inject(BoardsStore);
  });

  it('starts with the empty filter', () => {
    expect(store.filter()).toEqual(EMPTY_FILTER);
  });

  it('setFilter() patches only the given fields', () => {
    store.setFilter({ text: 'login' });
    store.setFilter({ labelIds: ['l1'] });
    expect(store.filter()).toEqual({ ...EMPTY_FILTER, text: 'login', labelIds: ['l1'] });
  });

  it('clearFilter() resets to the empty filter', () => {
    store.setFilter({ text: 'x', assignee: 'me' });
    store.clearFilter();
    expect(store.filter()).toEqual(EMPTY_FILTER);
  });
});
