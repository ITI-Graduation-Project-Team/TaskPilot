using System.Linq.Expressions;

namespace TaskPilot.Data.Repositories
{
    public interface IRepository<T> where T : class
    {
        IQueryable<T> GetQueryable();

        // ──────────────────────────── Single Entity Queries ────────────────────────────

        /// <summary>
        /// Gets a single entity by its primary key (Guid).
        /// </summary>
        Task<T?> GetByIdAsync(Guid id);

        /// <summary>
        /// Gets a single entity by its primary key, eagerly loading the specified navigation properties.
        /// </summary>
        Task<T?> GetByIdAsync(Guid id, params Expression<Func<T, object>>[] includes);

        /// <summary>
        /// Gets the first entity that matches the predicate, or null.
        /// </summary>
        Task<T?> FindSingleAsync(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// Gets the first entity that matches the predicate with eager-loaded navigation properties, or null.
        /// </summary>
        Task<T?> FindSingleAsync(Expression<Func<T, bool>> predicate, params Expression<Func<T, object>>[] includes);

        // ──────────────────────────── Collection Queries ────────────────────────────

        /// <summary>
        /// Gets all entities in the set.
        /// </summary>
        Task<IEnumerable<T>> GetAllAsync();

        /// <summary>
        /// Gets all entities, eagerly loading the specified navigation properties.
        /// </summary>
        Task<IEnumerable<T>> GetAllAsync(params Expression<Func<T, object>>[] includes);

        /// <summary>
        /// Gets all entities that satisfy the predicate.
        /// </summary>
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// Gets all entities that satisfy the predicate, eagerly loading the specified navigation properties.
        /// </summary>
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, params Expression<Func<T, object>>[] includes);

        // ──────────────────────────── Existence / Count ────────────────────────────

        /// <summary>
        /// Returns true if at least one entity matches the predicate.
        /// </summary>
        Task<bool> AnyAsync(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// Returns the total number of entities.
        /// </summary>
        Task<int> CountAsync();

        /// <summary>
        /// Returns the number of entities that match the predicate.
        /// </summary>
        Task<int> CountAsync(Expression<Func<T, bool>> predicate);

        // ──────────────────────────── Add ────────────────────────────

        /// <summary>
        /// Adds a single entity to the set.
        /// </summary>
        Task AddAsync(T entity);

        /// <summary>
        /// Adds a collection of entities to the set.
        /// </summary>
        Task AddRangeAsync(IEnumerable<T> entities);

        // ──────────────────────────── Update ────────────────────────────

        /// <summary>
        /// Marks an entity as modified.
        /// </summary>
        void Update(T entity);

        /// <summary>
        /// Marks a collection of entities as modified.
        /// </summary>
        void UpdateRange(IEnumerable<T> entities);

        // ──────────────────────────── Delete ────────────────────────────

        /// <summary>
        /// Removes a single tracked entity from the set.
        /// </summary>
        void Delete(T entity);

        /// <summary>
        /// Removes a collection of tracked entities from the set.
        /// </summary>
        void DeleteRange(IEnumerable<T> entities);

        /// <summary>
        /// Finds and removes all entities that match the given predicate.
        /// Returns the number of entities deleted.
        /// </summary>
        Task<int> DeleteWhereAsync(Expression<Func<T, bool>> predicate);
    }
}
