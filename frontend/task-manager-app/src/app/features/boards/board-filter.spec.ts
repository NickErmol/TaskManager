import { makeTask } from '../../testing/factories';
import { applyFilter, EMPTY_FILTER, isFilterActive, toRealPosition } from './board-filter';

describe('applyFilter', () => {
  const me = 'me-id';

  it('returns all tasks for the empty filter', () => {
    const tasks = [makeTask(), makeTask()];
    expect(applyFilter(tasks, EMPTY_FILTER, me)).toEqual(tasks);
  });

  it('matches text against the title, case-insensitively', () => {
    const hit = makeTask({ title: 'Fix LOGIN bug' });
    const miss = makeTask({ title: 'Write docs' });
    expect(applyFilter([hit, miss], { ...EMPTY_FILTER, text: 'login' }, me)).toEqual([hit]);
  });

  it('label filter is OR within the selection', () => {
    const a = makeTask({ labelIds: ['l1'] });
    const b = makeTask({ labelIds: ['l2'] });
    const none = makeTask({ labelIds: [] });
    expect(applyFilter([a, b, none], { ...EMPTY_FILTER, labelIds: ['l1', 'l2'] }, me)).toEqual([a, b]);
  });

  it('filter kinds compose with AND', () => {
    const hit = makeTask({ title: 'login', labelIds: ['l1'], priority: 'High' });
    const wrongLabel = makeTask({ title: 'login', labelIds: ['l2'], priority: 'High' });
    const filter = { ...EMPTY_FILTER, text: 'login', labelIds: ['l1'], priority: 'High' as const };
    expect(applyFilter([hit, wrongLabel], filter, me)).toEqual([hit]);
  });

  it('assignee "me" keeps only my tasks; "unassigned" keeps unassigned ones', () => {
    const mine = makeTask({ assignedTo: me });
    const other = makeTask({ assignedTo: 'someone' });
    const free = makeTask({ assignedTo: null });
    expect(applyFilter([mine, other, free], { ...EMPTY_FILTER, assignee: 'me' }, me)).toEqual([mine]);
    expect(applyFilter([mine, other, free], { ...EMPTY_FILTER, assignee: 'unassigned' }, me)).toEqual([free]);
  });

  it('assignee "me" matches nothing when the current user is unknown', () => {
    const mine = makeTask({ assignedTo: 'me-id' });
    const free = makeTask({ assignedTo: null });
    expect(applyFilter([mine, free], { ...EMPTY_FILTER, assignee: 'me' }, null)).toEqual([]);
  });

  it('AND composition includes the assignee dimension', () => {
    const hit = makeTask({ title: 'login', assignedTo: me });
    const wrongAssignee = makeTask({ title: 'login', assignedTo: 'other' });
    expect(applyFilter([hit, wrongAssignee], { ...EMPTY_FILTER, text: 'login', assignee: 'me' }, me)).toEqual([hit]);
  });
});

describe('isFilterActive', () => {
  it('is false for the empty filter and true when any field is set', () => {
    expect(isFilterActive(EMPTY_FILTER)).toBe(false);
    expect(isFilterActive({ ...EMPTY_FILTER, text: 'x' })).toBe(true);
    expect(isFilterActive({ ...EMPTY_FILTER, labelIds: ['l1'] })).toBe(true);
    expect(isFilterActive({ ...EMPTY_FILTER, assignee: 'me' })).toBe(true);
    expect(isFilterActive({ ...EMPTY_FILTER, priority: 'Low' })).toBe(true);
  });

  it('treats whitespace-only text as inactive', () => {
    expect(isFilterActive({ ...EMPTY_FILTER, text: '   ' })).toBe(false);
  });
});

describe('toRealPosition', () => {
  // real column: [r0, r1, r2, r3]; visible (filtered): [r1, r3]
  const real = [makeTask(), makeTask(), makeTask(), makeTask()];
  const visible = [real[1], real[3]];

  it('maps a visible index to the real index of the task at that slot', () => {
    expect(toRealPosition(real, visible, 0)).toBe(1); // drop before r1
    expect(toRealPosition(real, visible, 1)).toBe(3); // drop before r3
  });

  it('maps an end-of-visible-list drop to the end of the real list', () => {
    expect(toRealPosition(real, visible, 2)).toBe(4);
  });

  it('is the identity when nothing is filtered out', () => {
    expect(toRealPosition(real, real, 2)).toBe(2);
  });

  it('throws when the anchor task is missing from the real column', () => {
    const alien = makeTask();
    expect(() => toRealPosition(real, [alien], 0)).toThrow('not in the column');
  });
});
