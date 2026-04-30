using TaskPilot.Application.Common.Errors;

namespace TaskPilot.Application.Common.Results
{
    /// <summary>
    /// Represents the outcome of an operation that does NOT return data.
    /// Either succeeds (no error) or fails (carries an Error).
    /// </summary>
    public class Result
    {
        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;
        public Error Error { get; }

        protected Result(bool isSuccess, Error error)
        {
            if (isSuccess && error != Error.None)
                throw new InvalidOperationException("A successful result cannot carry an error.");

            if (!isSuccess && error == Error.None)
                throw new InvalidOperationException("A failed result must carry an error.");

            IsSuccess = isSuccess;
            Error = error;
        }

        // ──────────────────────── Factory Methods ────────────────────────

        /// <summary>Creates a successful result with no data.</summary>
        public static Result Success() => new(true, Error.None);

        /// <summary>Creates a failed result from the given error.</summary>
        public static Result Failure(Error error) => new(false, error);

        /// <summary>Creates a successful result carrying data.</summary>
        public static Result<T> Success<T>(T value) => Result<T>.Success(value);

        /// <summary>Creates a failed result for a typed result.</summary>
        public static Result<T> Failure<T>(Error error) => Result<T>.Failure(error);
    }

    /// <summary>
    /// Represents the outcome of an operation that returns data of type <typeparamref name="T"/>.
    /// On success the Value is populated; on failure only the Error is available.
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

        private Result(T? value, bool isSuccess, Error error)
            : base(isSuccess, error)
        {
            _value = value;
        }

        // ──────────────────────── Factory Methods ────────────────────────

        public static Result<T> Success(T value) => new(value, true, Error.None);

        public new static Result<T> Failure(Error error) => new(default, false, error);

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
