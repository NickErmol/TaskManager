global using NSubstitute;
global using TaskManager.Tasks.Application.Commands;
global using TaskManager.Tasks.Application.DTOs;
global using TaskManager.Tasks.Application.Handlers;
global using TaskManager.Tasks.Application.Mappers;
global using TaskManager.Tasks.Application.Queries;
global using TaskManager.Tasks.Domain.Entities;
global using TaskManager.Tasks.Domain.Interfaces;
global using TaskManager.Tasks.Domain.ValueObjects;
// Same alias the production project uses — TaskStatus otherwise collides with System.Threading.Tasks.TaskStatus.
global using TaskStatus = TaskManager.Tasks.Domain.ValueObjects.TaskStatus;
