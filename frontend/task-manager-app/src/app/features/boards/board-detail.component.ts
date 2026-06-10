import { CdkDragDrop } from '@angular/cdk/drag-drop';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { BoardsStore } from './boards.store';
import { TaskDto, TaskStatus } from '../../core/models';

// Smart component — Step 7a skeleton; Kanban board lands in Step 7b.
@Component({
  selector: 'tm-board-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: ``,
})
export class BoardDetailComponent {
  protected readonly store = inject(BoardsStore);

  onDrop(event: CdkDragDrop<TaskDto[]>, newStatus: TaskStatus): void {}
}
