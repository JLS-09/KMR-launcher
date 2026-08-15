using System;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Exceptions;

public class NoCompatibleVersionFoundException : Exception
{
    public override string Message { get; }

    public NoCompatibleVersionFoundException(Relationship relationship)
    {
        Message =
            $"No compatible version found for relationship: {{ Name: {relationship.Name}, " +
            $"MinVersion: {relationship.MinVersion}, " +
            $"MaxVersion: {relationship.MaxVersion}, " +
            $"Version: {relationship.Version}, " +
            $"Comment: {relationship.Comment}, " +
            $"SuppressRecommendations: {relationship.SuppressRecommendations}}}";
    }
}