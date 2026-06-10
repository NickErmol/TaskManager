import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { patchState } from '@ngrx/signals';
import { BoardListComponent } from './board-list.component';
import { BoardsStore } from './boards.store';
import { makeBoard } from '../../testing/factories';

describe('BoardListComponent', () => {
  let fixture: ComponentFixture<BoardListComponent>;
  let store: InstanceType<typeof BoardsStore>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [BoardListComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]), provideNoopAnimations()],
    }).compileComponents();

    store = TestBed.inject(BoardsStore);
    jest.spyOn(store, 'loadBoards').mockResolvedValue();
    fixture = TestBed.createComponent(BoardListComponent);
    fixture.detectChanges();
  });

  it('loads boards on init', () => {
    expect(store.loadBoards).toHaveBeenCalled();
  });

  it('renders a card per board', () => {
    patchState(store, {
      boards: [makeBoard({ name: 'Sprint 1' }), makeBoard({ name: 'Backlog' })],
      isLoading: false,
    });
    fixture.detectChanges();

    const cards = fixture.nativeElement.querySelectorAll('[data-testid="board-card"]');
    expect(cards).toHaveLength(2);
    expect(fixture.nativeElement.textContent).toContain('Sprint 1');
    expect(fixture.nativeElement.textContent).toContain('Backlog');
  });

  it('submits the create form to the store via ngSubmit', () => {
    // regression: without [formGroup] on the form, (ngSubmit) never fires and the
    // native submit reloads the page — caught by the Step 8 E2E suite
    const createSpy = jest.spyOn(store, 'createBoard').mockResolvedValue(null);
    const input = fixture.nativeElement.querySelector('input[formcontrolname="name"]') as HTMLInputElement;
    input.value = 'Sprint 9';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    const form = fixture.nativeElement.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));

    expect(createSpy).toHaveBeenCalledWith({ name: 'Sprint 9' });
  });

  it('shows the empty state when there are no boards', () => {
    patchState(store, { boards: [], isLoading: false });
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('tm-empty-state')).toBeTruthy();
    expect(fixture.nativeElement.querySelectorAll('[data-testid="board-card"]')).toHaveLength(0);
  });
});
