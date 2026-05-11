using TaskPilot.Models.Common.Errors;

namespace TaskPilot.Models.Common.Results
{
    /// <summary>
    /// Represents the outcome of an operation that does NOT return data.
    /// Either succeeds (no error) or fails (carries one or more Errors).
    /// </summary>
    public class Result
    {
        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;

        /// <summary>The primary (first) error. Use <see cref="Errors"/> to access all errors.</summary>
        public Error Error => Errors.Count > 0 ? Errors[0] : Error.None;

        /// <summary>All errors. Contains one entry for single-error failures, multiple for validation failures.</summary>
        public IReadOnlyList<Error> Errors { get; }

        protected Result(bool isSuccess, IReadOnlyList<Error> errors)
        {
            if (isSuccess && errors.Any(e => e != Error.None))
                throw new InvalidOperationException("A successful result cannot carry errors.");

            if (!isSuccess && errors.Count == 0)
                throw new InvalidOperationException("A failed result must carry at least one error.");

            IsSuccess = isSuccess;
            Errors = errors;
        }

        // ──────────────────────── Factory Methods ────────────────────────

        /// <summary>Creates a successful result with no data.</summary>
        public static Result Success() => new(true, []);

        /// <summary>Creates a failed result from a single error.</summary>
        public static Result Failure(Error error) => new(false, [error]);

        /// <summary>Creates a failed result from multiple errors (e.g. validation failures).</summary>
        public static Result Failure(IEnumerable<Error> errors)
        {
            var list = errors.ToList();
            if (list.Count == 0)
                throw new ArgumentException("At least one error must be provided.", nameof(errors));
            return new(false, list);
        }

        /// <summary>Creates a successful result carrying data.</summary>
        public static Result<T> Success<T>(T value) => Result<T>.Success(value);

        /// <summary>Creates a failed typed result from a single error.</summary>
        public static Result<T> Failure<T>(Error error) => Result<T>.Failure(error);

        /// <summary>Creates a failed typed result from multiple errors.</summary>
        public static Result<T> Failure<T>(IEnumerable<Error> errors) => Result<T>.Failure(errors);
    }

    /// <summary>
    /// Represents the outcome of an operation that returns data of type <typeparamref name="T"/>.
    /// On success the Value is populated; on failure only Errors are available.
    /// </summary>
    public class Result<T> : Result
    {
        private readonly T? _value;

        /// <summary>
        /// The data payload. Throws if the result is a failure — always check IsSuccess first.
        /// </summary>
        public T Value => IsSuccess
            ? _value!
            : throw new InvalidOperationException("Cannot access Value on a failed result. Check IsSuccess first.");

        private Result(T? value, bool isSuccess, IReadOnlyList<Error> errors)
            : base(isSuccess, errors)
        {
            _value = value;
        }

        // ──────────────────────── Factory Methods ────────────────────────

        public static Result<T> Success(T value) => new(value, true, []);

        public new static Result<T> Failure(Error error) => new(default, false, [error]);

        public new static Result<T> Failure(IEnumerable<Error> errors)
        {
            var list = errors.ToList();
            if (list.Count == 0)
                throw new ArgumentException("At least one error must be provided.", nameof(errors));
            return new(default, false, list);
        }

        // ──────────────────────── Implicit Conversions ────────────────────────

        /// <summary>
        /// Allows returning a value directly where a Result&lt;T&gt; is expected.
        /// <code>return myEntity;  // implicitly wraps in Result&lt;T&gt;.Success</code>
        /// </summary>
        public static implicit operator Result<T>(T value) => Success(value);

        /// <summary>
        /// Allows returning an Error directly where a Result&lt;T&gt; is expected.
        /// <code>return CommonErrors.NotFound("User");  // implicitly wraps in Result&lt;T&gt;.Failure</code>
        /// </summary>
        public static implicit operator Result<T>(Error error) => Failure(error);
    }
}
