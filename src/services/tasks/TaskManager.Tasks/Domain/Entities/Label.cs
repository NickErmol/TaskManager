using FluentResults;
using TaskManager.Tasks.Domain.ValueObjects;

namespace TaskManager.Tasks.Domain.Entities;

public class Label
{
    public Guid Id { get; private set; }
    public Guid BoardId { get; private set; }
    public string Name { get; private set; } = default!;
    public Color Color { get; private set; } = default!;

    private Label() { }

    public static Result<Label> Create(Guid boardId, string name, string colorHex)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Fail<Label>("Label name is required");
        if (name.Length > 30)
            return Result.Fail<Label>("Label name must be 30 characters or fewer");

        var colorResult = Color.Create(colorHex);
        if (colorResult.IsFailed)
            return Result.Fail<Label>(colorResult.Errors);

        return Result.Ok(new Label
        {
            Id = Guid.NewGuid(),
            BoardId = boardId,
            Name = name,
            Color = colorResult.Value,
        });
    }

    public void Rename(string name) => Name = name;
}
