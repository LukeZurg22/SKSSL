using System;

namespace SKSSL.Exceptions;

/// <inheritdoc />
/// Thrown when a statistics list was found empty when it should not be.
public class EmptyStatisticsListException(string s) : Exception(s);

/// <inheritdoc />
/// A statistic that should have been there, was not present.
public class MissingStatisticException(string s) : Exception(s);

/// <inheritdoc />
/// Thrown when an evaluation was found to be recursive when it should not be.
public class RecursiveEvaluateException(string s) : Exception(s);

/// <inheritdoc />
/// Thrown by exceptions involving Entities, the Spawning thereof, or around other methods acting upon them.
public class EntityException(string s) : Exception(s);
public class RegistryException(string s) : Exception(s);