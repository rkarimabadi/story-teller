namespace StoryTeller.UI.Domain;
#region Enums
public enum TableType
{
    Archetype = 1,
    Situation = 2,
    Twist = 3
}
#endregion Enums

#region ValueObjects
public record DieRoll
{
    public int Value { get; }

    public DieRoll(int value)
    {
        if (value < 1 || value > 6)
            throw new ArgumentOutOfRangeException(nameof(value), "باید عددی بین 1 و 6 باشد!");
        Value = value;
    }

    public static DieRoll FromInt(int value) => new(value);
}
#endregion ValueObjects

#region Entities
public class Table
{
    public Guid Id { get; private set; }
    public TableType Type { get; private set; }
    public string Name { get; private set; }
    private List<string> _items = new();
    public IReadOnlyList<string> Items => _items.AsReadOnly();

    public Table(Guid id, TableType type, string name, IEnumerable<string> items)
    {
        Id = id;
        Type = type;
        Name = name;
        SetItems(items);
    }

    public void SetItems(IEnumerable<string> newItems)
    {
        var list = newItems.ToList();
        if (list.Count != 6)
            throw new DomainException("جدول باید شامل 6 آیتم باشد!");
        _items = list;
        AddDomainEvent(new TableUpdatedEvent(Id, Type, _items));
    }

    public string GetItemByIndex(int index) // index 0-based
    {
        if (index < 0 || index >= _items.Count)
            throw new DomainException("شناسه خارج از بازه مجاز است!");
        return _items[index];
    }

    public string GetItemByRoll(DieRoll roll) => GetItemByIndex(roll.Value - 1);

    private List<IDomainEvent> _domainEvents = new();
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    protected void AddDomainEvent(IDomainEvent eventItem) => _domainEvents.Add(eventItem);
    public void ClearEvents() => _domainEvents.Clear();
}
#endregion Entities

#region Services
public interface IDiceRoller
{
    DieRoll Roll();
}
#endregion Services

#region Exceptions
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}
#endregion Exceptions

#region Events
public record TableUpdatedEvent(Guid TableId, TableType Type, IReadOnlyList<string> Items) : IDomainEvent;

public interface IDomainEvent { }
#endregion Events