namespace BO;

/// <summary>
/// Exception thrown when a requested resource or service is temporarily unavailable.
/// </summary>
/// <remarks>
/// This exception is used to indicate transient failures that may be resolved by retrying the operation.
/// Common scenarios include database connection timeouts, service overload, or temporary maintenance windows.
/// This exception is serializable to support cross-AppDomain and remote communication scenarios.
/// </remarks>
[Serializable]
public class BLTemporaryNotAvailableException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BLTemporaryNotAvailableException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public BLTemporaryNotAvailableException(string? message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="BLTemporaryNotAvailableException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or <c>null</c> if no inner exception is specified.</param>
    public BLTemporaryNotAvailableException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception thrown when attempting to create or add a resource that already exists in the system.
/// </summary>
/// <remarks>
/// This exception is used to indicate that a duplicate resource creation operation has been attempted.
/// Common scenarios include creating a courier or order with an ID that already exists, or registering
/// a user with an email that is already registered.
/// This exception is serializable to support cross-AppDomain and remote communication scenarios.
/// </remarks>
[Serializable]
public class BLAlreadyExistsException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BLAlreadyExistsException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public BLAlreadyExistsException(string? message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="BLAlreadyExistsException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or <c>null</c> if no inner exception is specified.</param>
    public BLAlreadyExistsException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception thrown when a requested resource is not found in the system.
/// </summary>
/// <remarks>
/// This exception is used to indicate that a resource lookup operation failed because the requested
/// resource does not exist. Common scenarios include retrieving a non-existent order, courier, or configuration.
/// This exception is serializable to support cross-AppDomain and remote communication scenarios.
/// </remarks>
[Serializable]
public class BLNotFoundException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BLNotFoundException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public BLNotFoundException(string? message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="BLNotFoundException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or <c>null</c> if no inner exception is specified.</param>
    public BLNotFoundException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception thrown when user-supplied input is invalid or fails validation rules.
/// </summary>
/// <remarks>
/// This exception is used to indicate that the provided input data does not meet the system's requirements.
/// Common scenarios include invalid email formats, negative numbers where positive values are required,
/// null values for non-nullable fields, or values outside acceptable ranges.
/// This exception is serializable to support cross-AppDomain and remote communication scenarios.
/// </remarks>
[Serializable]
public class BLInvalidInputException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BLInvalidInputException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public BLInvalidInputException(string? message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="BLInvalidInputException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or <c>null</c> if no inner exception is specified.</param>
    public BLInvalidInputException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception thrown when a user attempts to perform an operation without the required authorization or permissions.
/// </summary>
/// <remarks>
/// This exception is used to indicate that the current user does not have sufficient privileges to perform
/// the requested operation. Common scenarios include a courier attempting to modify another courier's details,
/// or a non-director attempting to promote someone to director status.
/// This exception is serializable to support cross-AppDomain and remote communication scenarios.
/// </remarks>
[Serializable]
public class BLUnauthorizedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BLUnauthorizedException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public BLUnauthorizedException(string? message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="BLUnauthorizedException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or <c>null</c> if no inner exception is specified.</param>
    public BLUnauthorizedException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception thrown when an operation cannot be performed because the system state does not permit it.
/// </summary>
/// <remarks>
/// This exception is used to indicate that while the operation itself is valid, the current state of the system
/// or affected resources prevents it from being executed. Common scenarios include canceling an order that has
/// already been delivered, or assigning an order that is already assigned to another courier.
/// This exception is serializable to support cross-AppDomain and remote communication scenarios.
/// </remarks>
[Serializable]
public class BLInvalidOperationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BLInvalidOperationException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public BLInvalidOperationException(string? message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="BLInvalidOperationException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or <c>null</c> if no inner exception is specified.</param>
    public BLInvalidOperationException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception thrown when a business logic operation fails to complete successfully.
/// </summary>
/// <remarks>
/// This exception is used to indicate that an operation has failed during execution, typically due to an
/// unexpected error or failure in the underlying data access or processing layers. This is a general-purpose
/// exception for operation failures that don't fit into other specific exception categories.
/// This exception is serializable to support cross-AppDomain and remote communication scenarios.
/// </remarks>
[Serializable]
public class BLFailedOperation : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BLFailedOperation"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public BLFailedOperation(string? message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="BLFailedOperation"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or <c>null</c> if no inner exception is specified.</param>
    public BLFailedOperation(string message, Exception innerException) : base(message, innerException) { }
}