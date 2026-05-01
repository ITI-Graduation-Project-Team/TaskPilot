namespace TaskPilot.Models.Common
{
    public abstract class BaseEntity<TId> 
    {
        public TId Id { get; protected set; }

        //public override bool Equals(object? obj)
        //{
        //    if (obj is not BaseEntity<TId> other)
        //        return false;

        //    if (GetType() != other.GetType())
        //        return false;

        //    if (EqualityComparer<TId>.Default.Equals(Id, default) ||
        //    EqualityComparer<TId>.Default.Equals(other.Id, default))
        //        return false;

        //    return EqualityComparer<TId>.Default.Equals(Id, other.Id);

        //}

        //public override int GetHashCode()
        //{
        //    return HashCode.Combine(GetType(), Id);
        //}
    }

    public abstract class BaseEntity : BaseEntity<Guid>
    {
    }
}
