// Tasks-service-wide type aliases. The spec uses `TaskStatus` (spec §4.3 value objects);
// that conflicts with System.Threading.Tasks.TaskStatus pulled in by ImplicitUsings, so we
// fix the alias once here rather than in every file.
global using TaskStatus = TaskManager.Tasks.Domain.ValueObjects.TaskStatus;
